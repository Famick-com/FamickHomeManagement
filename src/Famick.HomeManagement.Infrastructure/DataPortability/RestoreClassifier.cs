using System.Data.Common;
using System.Text.Json;
using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// What the household already has for a set of archived ids.
/// </summary>
/// <param name="Id">The row's primary key.</param>
/// <param name="UpdatedAt">When it last changed here, or its creation time if never.</param>
/// <param name="TenantId">
/// Which household owns it. Should always be this one — the archive is this household's — but it
/// is read rather than assumed, because the one thing worse than refusing a restore is writing
/// into somebody else's home.
/// </param>
public sealed record ExistingRow(Guid Id, DateTime? UpdatedAt, Guid? TenantId);

/// <summary>
/// Decides, per archived row, whether it is missing here, unchanged, or changed since the backup.
/// </summary>
public sealed class RestoreClassifier
{
    /// <summary>How many ids to ask about at once.</summary>
    private const int ChunkSize = 2000;

    /// <summary>
    /// Looks up which of <paramref name="ids"/> already exist, and when they last changed.
    /// </summary>
    /// <remarks>
    /// Deliberately reads across the tenant boundary — no filter on TenantId — and returns the
    /// owner so the caller can check it. Every primary key in this database is globally unique
    /// rather than per-household, so an id that belongs to another household is a genuine
    /// collision rather than a match, and quietly filtering it away would turn a refusal into an
    /// overwrite of the wrong row. This is the only cross-household read in the feature, and it
    /// returns ids and owners, never content.
    /// </remarks>
    public async Task<Dictionary<Guid, ExistingRow>> FindExistingAsync(
        DbConnection connection,
        IEntityType entityType,
        IReadOnlyList<Guid> ids,
        CancellationToken ct)
    {
        var found = new Dictionary<Guid, ExistingRow>();
        if (ids.Count == 0) return found;

        var table = TenantDataModel.Quote(entityType);
        var idColumn = TenantDataModel.Quote(ColumnFor(entityType, nameof(BaseEntity.Id))!);
        var updatedColumn = ColumnFor(entityType, nameof(BaseEntity.UpdatedAt));
        var createdColumn = ColumnFor(entityType, nameof(BaseEntity.CreatedAt));
        var tenantColumn = TenantDataModel.TenantColumnName(entityType);

        // UpdatedAt is nullable — a row never edited since creation has none — so fall back to
        // CreatedAt rather than treating "never touched" as "older than anything".
        var timestamp = updatedColumn == null
            ? (createdColumn == null ? "NULL" : TenantDataModel.Quote(createdColumn))
            : createdColumn == null
                ? TenantDataModel.Quote(updatedColumn)
                : $"COALESCE({TenantDataModel.Quote(updatedColumn)}, {TenantDataModel.Quote(createdColumn)})";

        var tenantProjection = tenantColumn == null ? "NULL" : TenantDataModel.Quote(tenantColumn);

        foreach (var chunk in ids.Chunk(ChunkSize))
        {
            ct.ThrowIfCancellationRequested();

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"SELECT {idColumn}, {timestamp}, {tenantProjection} FROM {table} WHERE {idColumn} = ANY(@ids)";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@ids";
            parameter.Value = chunk;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(ct);

            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0);
                var updatedAt = await reader.IsDBNullAsync(1, ct) ? (DateTime?)null : reader.GetDateTime(1);
                var tenantId = await reader.IsDBNullAsync(2, ct) ? (Guid?)null : reader.GetGuid(2);

                found[id] = new ExistingRow(id, updatedAt, tenantId);
            }
        }

        return found;
    }

    /// <summary>
    /// Classifies one archived row against what the household has.
    /// </summary>
    public RestoreClassification Classify(
        StagedRow staged,
        ExistingRow? existing,
        Guid tenantId,
        out DateTime? sourceUpdatedAt,
        out DateTime? targetUpdatedAt)
    {
        sourceUpdatedAt = ReadTimestamp(staged);
        targetUpdatedAt = existing?.UpdatedAt;

        // Nothing here by that id: deleted since the backup, or never existed.
        if (existing == null) return RestoreClassification.Restored;

        // Someone else's row wearing this id. Unreachable given the archive belongs to this
        // household, and checked anyway — this is the branch that would otherwise overwrite
        // another home's data.
        if (existing.TenantId.HasValue && existing.TenantId.Value != tenantId)
            return RestoreClassification.Invalid;

        if (sourceUpdatedAt is null || targetUpdatedAt is null)
            return RestoreClassification.Unchanged;

        // Edited here after the archive was taken. The only class that needs a person, because
        // overwriting it would silently discard work done since the backup.
        return targetUpdatedAt > sourceUpdatedAt
            ? RestoreClassification.ChangedSince
            : RestoreClassification.Unchanged;
    }

    /// <summary>
    /// A label a person can recognise, for the review list.
    /// </summary>
    public static string? LabelFor(StagedRow row)
    {
        foreach (var candidate in new[] { "Name", "Title", "DisplayName", "FirstName", "Description" })
        {
            if (row.Values.TryGetValue(candidate, out var value) &&
                value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text)) return text.Length > 200 ? text[..200] : text;
            }
        }

        return null;
    }

    private static DateTime? ReadTimestamp(StagedRow row)
    {
        if (row.Values.TryGetValue(nameof(BaseEntity.UpdatedAt), out var updated) &&
            updated.ValueKind != JsonValueKind.Null &&
            updated.TryGetDateTime(out var updatedAt))
        {
            return updatedAt;
        }

        return row.Values.TryGetValue(nameof(BaseEntity.CreatedAt), out var created) &&
               created.TryGetDateTime(out var createdAt)
            ? createdAt
            : null;
    }

    private static string? ColumnFor(IEntityType entityType, string propertyName)
    {
        var property = entityType.FindProperty(propertyName);
        return property == null ? null : TenantDataModel.ColumnName(entityType, property);
    }
}
