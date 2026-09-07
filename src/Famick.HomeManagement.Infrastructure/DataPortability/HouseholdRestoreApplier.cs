using System.Data.Common;
using System.Text.Json;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// What a restore did to one table.
/// </summary>
public sealed record RestoreTableOutcome(string Entity, int Inserted, int Updated, int Skipped, int Failed);

/// <summary>
/// Writes archived rows back into the household.
/// </summary>
/// <remarks>
/// <para>
/// Bypasses the change tracker entirely, using parameterised INSERT and UPDATE on the caller's
/// connection. That is not an optimisation. <c>HomeManagementDbContext.UpdateEntityTimestamps</c>
/// overwrites <c>CreatedAt</c> on every added entity, so anything routed through SaveChanges would
/// silently stamp the restore's own clock over the household's history — the one thing a restore
/// exists to bring back.
/// </para>
/// <para>
/// Restore only ever adds and overwrites. A row created since the archive was taken is simply not
/// in it, and is left alone. Making the household match the archive exactly would mean deleting
/// those, which is a different and far more dangerous operation.
/// </para>
/// </remarks>
public sealed class HouseholdRestoreApplier(ILogger<HouseholdRestoreApplier> logger)
{
    /// <summary>
    /// Inserts and updates one table's rows.
    /// </summary>
    /// <param name="deferredColumns">
    /// Nullable foreign keys pointing at other restored tables, left null on insert and filled in
    /// by <see cref="PatchDeferredReferencesAsync"/> once every table is present.
    /// </param>
    public async Task<RestoreTableOutcome> ApplyTableAsync(
        DbConnection connection,
        DbTransaction transaction,
        IEntityType entityType,
        Guid tenantId,
        IReadOnlyList<(StagedRow Row, RestoreClassification Classification, bool Overwrite)> rows,
        IReadOnlySet<string> deferredColumns,
        CancellationToken ct)
    {
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var failed = 0;

        var writable = WritableProperties(entityType).ToList();
        var tenantProperty = entityType.FindProperty(nameof(ITenantEntity.TenantId));

        foreach (var (row, classification, overwrite) in rows)
        {
            ct.ThrowIfCancellationRequested();

            if (classification == RestoreClassification.Invalid) { failed++; continue; }
            if (classification == RestoreClassification.Unchanged) { skipped++; continue; }
            if (classification == RestoreClassification.ChangedSince && !overwrite) { skipped++; continue; }

            try
            {
                if (classification == RestoreClassification.Restored)
                {
                    await InsertAsync(connection, transaction, entityType, row, writable,
                        tenantProperty, tenantId, deferredColumns, ct);
                    inserted++;
                }
                else
                {
                    await UpdateAsync(connection, transaction, entityType, row, writable,
                        tenantProperty, deferredColumns, ct);
                    updated++;
                }
            }
            catch (DbException ex)
            {
                // One row that will not go back must not cost the household the rest of the
                // restore. It is counted and reported rather than thrown.
                logger.LogWarning(ex, "Could not restore {Entity} {Id}", entityType.ClrType.Name, row.Id);
                failed++;
            }
        }

        return new RestoreTableOutcome(entityType.ClrType.Name, inserted, updated, skipped, failed);
    }

    /// <summary>
    /// Fills in the references left null during insert.
    /// </summary>
    /// <remarks>
    /// Two things need this. <c>User.ContactId</c> and <c>Contact</c> point at each other, so
    /// neither can be inserted first with both links standing. And <c>Contact.ParentContactId</c>
    /// points at its own table, where a Restrict rule is checked per row and refuses a parent whose
    /// children are in the same statement.
    /// </remarks>
    public async Task<int> PatchDeferredReferencesAsync(
        DbConnection connection,
        DbTransaction transaction,
        IEntityType entityType,
        IReadOnlyList<StagedRow> insertedRows,
        IReadOnlySet<string> deferredColumns,
        CancellationToken ct)
    {
        if (deferredColumns.Count == 0 || insertedRows.Count == 0) return 0;

        var patched = 0;
        var idColumn = ColumnFor(entityType, nameof(BaseEntity.Id))!;

        foreach (var row in insertedRows)
        {
            var assignments = new List<string>();
            var values = new List<object?>();

            foreach (var property in WritableProperties(entityType))
            {
                var column = TenantDataModel.ColumnName(entityType, property)!;
                if (!deferredColumns.Contains(column)) continue;
                if (!row.Values.TryGetValue(property.Name, out var value)) continue;
                if (value.ValueKind == JsonValueKind.Null) continue;

                assignments.Add($"{TenantDataModel.Quote(column)} = @p{values.Count}");
                values.Add(Convert(value, property.ClrType));
            }

            if (assignments.Count == 0) continue;

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                $"UPDATE {TenantDataModel.Quote(entityType)} SET {string.Join(", ", assignments)} " +
                $"WHERE {TenantDataModel.Quote(idColumn)} = @id";

            for (var i = 0; i < values.Count; i++)
                AddParameter(command, $"@p{i}", values[i]);
            AddParameter(command, "@id", row.Id);

            try
            {
                patched += await command.ExecuteNonQueryAsync(ct);
            }
            catch (DbException ex)
            {
                // A reference to something that was not restored — the row it pointed at is gone
                // and not in the archive. Leaving it null is the honest outcome.
                logger.LogWarning(ex, "Could not repoint {Entity} {Id}", entityType.ClrType.Name, row.Id);
            }
        }

        return patched;
    }

    /// <summary>
    /// The nullable foreign keys on <paramref name="entityType"/> that point at another table in
    /// the restore set, and therefore cannot be written until everything is present.
    /// </summary>
    public static IReadOnlySet<string> DeferredColumnsFor(IEntityType entityType, ISet<IEntityType> inScope) =>
        TenantDataModel.OptionalReferenceColumns(entityType, inScope).ToHashSet();

    private async Task InsertAsync(
        DbConnection connection, DbTransaction transaction, IEntityType entityType, StagedRow row,
        IReadOnlyList<IProperty> writable, IProperty? tenantProperty, Guid tenantId,
        IReadOnlySet<string> deferredColumns, CancellationToken ct)
    {
        var columns = new List<string>();
        var placeholders = new List<string>();
        var values = new List<object?>();

        foreach (var property in writable)
        {
            var column = TenantDataModel.ColumnName(entityType, property)!;

            object? value;

            if (property == tenantProperty)
            {
                // Never taken from the archive. A self-hosted install and a cloud household do not
                // share a tenant id, and a row written under the wrong one is invisible to every
                // query the household makes.
                value = tenantId;
            }
            else if (deferredColumns.Contains(column))
            {
                value = null;
            }
            else if (row.Values.TryGetValue(property.Name, out var raw))
            {
                value = Convert(raw, property.ClrType);
            }
            else
            {
                // A column the archive predates. Left to the database default rather than guessed.
                continue;
            }

            columns.Add(TenantDataModel.Quote(column));
            placeholders.Add($"@p{values.Count}");
            values.Add(value);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO {TenantDataModel.Quote(entityType)} ({string.Join(", ", columns)}) " +
            $"VALUES ({string.Join(", ", placeholders)})";

        for (var i = 0; i < values.Count; i++)
            AddParameter(command, $"@p{i}", values[i]);

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task UpdateAsync(
        DbConnection connection, DbTransaction transaction, IEntityType entityType, StagedRow row,
        IReadOnlyList<IProperty> writable, IProperty? tenantProperty,
        IReadOnlySet<string> deferredColumns, CancellationToken ct)
    {
        var assignments = new List<string>();
        var values = new List<object?>();
        var idColumn = ColumnFor(entityType, nameof(BaseEntity.Id))!;

        foreach (var property in writable)
        {
            var column = TenantDataModel.ColumnName(entityType, property)!;

            // Identity, ownership and creation time are not the archive's to change on a row that
            // already exists here.
            if (column == idColumn) continue;
            if (property == tenantProperty) continue;
            if (property.Name == nameof(BaseEntity.CreatedAt)) continue;
            if (deferredColumns.Contains(column)) continue;

            if (!row.Values.TryGetValue(property.Name, out var raw)) continue;

            assignments.Add($"{TenantDataModel.Quote(column)} = @p{values.Count}");
            values.Add(Convert(raw, property.ClrType));
        }

        if (assignments.Count == 0) return;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"UPDATE {TenantDataModel.Quote(entityType)} SET {string.Join(", ", assignments)} " +
            $"WHERE {TenantDataModel.Quote(idColumn)} = @id";

        for (var i = 0; i < values.Count; i++)
            AddParameter(command, $"@p{i}", values[i]);
        AddParameter(command, "@id", row.Id);

        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// The columns a restore writes explicitly.
    /// </summary>
    /// <remarks>
    /// Value-generated columns are deliberately included. Guid primary keys are configured
    /// <c>ValueGeneratedOnAdd</c>, so filtering them out would leave the key off the INSERT — and
    /// preserving the original key is the whole of what makes this a restore rather than a copy.
    /// The archive already holds a value for every one of them.
    ///
    /// Computed columns are the real exclusion: the database derives those and refuses to be told
    /// what they are.
    /// </remarks>
    private static IEnumerable<IProperty> WritableProperties(IEntityType entityType)
    {
        var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);

        return entityType.GetProperties()
            .Where(p => !p.IsShadowProperty())
            .Where(p => TenantDataModel.ColumnName(entityType, p) != null)
            .Where(p => !storeObject.HasValue || p.GetComputedColumnSql(storeObject.Value) == null);
    }

    private static string? ColumnFor(IEntityType entityType, string propertyName)
    {
        var property = entityType.FindProperty(propertyName);
        return property == null ? null : TenantDataModel.ColumnName(entityType, property);
    }

    private static void AddParameter(DbCommand command, string name, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    /// <summary>
    /// Turns a JSON value back into what the column expects.
    /// </summary>
    private static object? Convert(JsonElement value, Type target)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;

        var underlying = Nullable.GetUnderlyingType(target) ?? target;

        if (underlying == typeof(Guid)) return value.TryGetGuid(out var guid) ? guid : null;
        if (underlying == typeof(DateTime)) return value.TryGetDateTime(out var date) ? date : null;
        if (underlying == typeof(DateTimeOffset)) return value.TryGetDateTimeOffset(out var offset) ? offset : null;
        if (underlying == typeof(bool)) return value.ValueKind == JsonValueKind.True;
        if (underlying == typeof(int)) return value.TryGetInt32(out var i) ? i : null;
        if (underlying == typeof(long)) return value.TryGetInt64(out var l) ? l : null;
        if (underlying == typeof(decimal)) return value.TryGetDecimal(out var m) ? m : null;
        if (underlying == typeof(double)) return value.TryGetDouble(out var d) ? d : null;
        if (underlying == typeof(float)) return value.TryGetSingle(out var f) ? f : null;
        if (underlying == typeof(byte[])) return value.ValueKind == JsonValueKind.String ? value.GetBytesFromBase64() : null;

        // Enums travel as whatever the provider wrote them out as — an integer for most, text for
        // the ones configured with a string conversion — and have to go back the same way. Sending
        // "3" to an integer column is rejected outright rather than coerced.
        if (underlying.IsEnum)
        {
            return value.ValueKind == JsonValueKind.Number
                ? value.TryGetInt32(out var ordinal) ? ordinal : null
                : value.GetString();
        }

        if (underlying == typeof(string))
            return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();

        // Anything else the model uses — a jsonb document, an array, a provider-specific type —
        // goes back as the text it was written as.
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
    }
}
