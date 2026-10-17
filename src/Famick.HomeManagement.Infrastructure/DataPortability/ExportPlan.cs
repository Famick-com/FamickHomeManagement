using Microsoft.EntityFrameworkCore;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// One table's place in an archive: which columns travel, and the query that reads this
/// household's rows of it.
/// </summary>
public sealed class ExportTable
{
    public required IEntityType EntityType { get; init; }
    public required int Order { get; init; }

    /// <summary>Property name paired with the physical column, in the order they are read.</summary>
    public required IReadOnlyList<(string Property, string Column)> Columns { get; init; }

    public required IReadOnlyList<string> ExcludedColumns { get; init; }

    /// <summary>Parameterised on the tenant id; never interpolated.</summary>
    public required string Sql { get; init; }

    public required string TenantScope { get; init; }
    public required IReadOnlyList<string> TenantPath { get; init; }

    public string EntityName => EntityType.ClrType.Name;
    public string TableName => EntityType.GetTableName()!;
    public string FileName { get; init; } = string.Empty;
}

/// <summary>
/// Works out what an archive will contain, before anything is read.
/// </summary>
public static class ExportPlan
{
    /// <summary>
    /// Builds the ordered set of tables to export for one household.
    /// </summary>
    /// <remarks>
    /// Write order, not delete order: an archive is read back top to bottom, so a row's parents
    /// have to appear before it does.
    /// </remarks>
    public static IReadOnlyList<ExportTable> Build(IModel model)
    {
        var tables = new List<ExportTable>();
        var order = 0;

        foreach (var entityType in TenantDataModel.EntityTypesInWriteOrder(model))
        {
            if (ExportRegistry.For(entityType.ClrType).Export != ExportDisposition.Export)
                continue;

            var excluded = ExportRegistry.ColumnsExcludedFrom(entityType.ClrType);

            var columns = entityType.GetProperties()
                .Where(p => !excluded.Contains(p.Name))
                .Select(p => (Property: p.Name, Column: TenantDataModel.ColumnName(entityType, p)))
                .Where(c => c.Column != null)
                .Select(c => (c.Property, Column: c.Column!))
                .ToList();

            var path = TenantReachability.TenantJoinPath(model, entityType);

            order++;
            tables.Add(new ExportTable
            {
                EntityType = entityType,
                Order = order,
                Columns = columns,
                ExcludedColumns = excluded.ToList(),
                Sql = BuildSql(entityType, columns, path),
                TenantScope = path.Count == 0 ? "own" : "viaParent",
                TenantPath = path.Select(fk => fk.PrincipalEntityType.ClrType.Name).ToList(),
                FileName = $"data/{order:D3}.{entityType.GetTableName()}.jsonl",
            });
        }

        return tables;
    }

    /// <summary>
    /// The types left out, with the reason, for the manifest.
    /// </summary>
    public static IReadOnlyList<ArchiveExclusion> Exclusions(IModel model) =>
        TenantReachability.TenantScopedEntityTypes(model)
            .Select(t => (Type: t, Disposition: ExportRegistry.For(t.ClrType)))
            .Where(x => x.Disposition.Export != ExportDisposition.Export)
            .Select(x => new ArchiveExclusion(
                x.Type.ClrType.Name,
                x.Disposition.Export.ToString(),
                x.Disposition.Reason))
            .OrderBy(x => x.Entity, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Builds a read scoped to one household.
    /// </summary>
    /// <remarks>
    /// Written as SQL with an explicit predicate rather than leaning on the global query filter.
    /// The filter treats a null tenant context as "no filter" and returns every household's rows,
    /// so a background worker that forgot to set the tenant would quietly archive the whole
    /// platform. Here the predicate is the query: there is no configuration under which it is
    /// absent, and the tenant id is a parameter, never interpolated.
    /// </remarks>
    private static string BuildSql(
        IEntityType entityType,
        IReadOnlyList<(string Property, string Column)> columns,
        IReadOnlyList<IForeignKey> tenantPath)
    {
        var contactScope = ContactScope.For(entityType);

        var projection = string.Join(", ", columns.Select(c => contactScope.Project(c.Column)));
        var sql = $"SELECT {projection} FROM {TenantDataModel.Quote(entityType)} t0";

        if (tenantPath.Count == 0)
        {
            var tenantColumn = TenantDataModel.TenantColumnName(entityType)
                ?? throw new InvalidOperationException(
                    $"'{entityType.Name}' has no join path to a tenant and no TenantId column of " +
                    "its own, so no household-scoped read of it is possible.");

            return $"{sql} WHERE t0.{TenantDataModel.Quote(tenantColumn)} = {{0}}{contactScope.Predicate}";
        }

        // Walk up the required foreign keys, aliasing each hop, so the predicate lands on the
        // nearest table that actually carries TenantId.
        var alias = 0;
        foreach (var fk in tenantPath)
        {
            var principal = fk.PrincipalEntityType;
            var from = $"t{alias}";
            var to = $"t{++alias}";

            var on = string.Join(" AND ", fk.Properties.Zip(fk.PrincipalKey.Properties, (dependent, key) =>
                $"{from}.{TenantDataModel.Quote(TenantDataModel.ColumnName(fk.DeclaringEntityType, dependent)!)} = " +
                $"{to}.{TenantDataModel.Quote(TenantDataModel.ColumnName(principal, key)!)}"));

            sql += $" JOIN {TenantDataModel.Quote(principal)} {to} ON {on}";
        }

        var finalType = tenantPath[^1].PrincipalEntityType;
        var finalTenantColumn = TenantDataModel.TenantColumnName(finalType)!;

        return $"{sql} WHERE t{alias}.{TenantDataModel.Quote(finalTenantColumn)} = {{0}}{contactScope.Predicate}";
    }

    /// <summary>
    /// Keeps contacts that are not shared with the whole household out of the archive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An archive is a household artifact: any member can start one, a run already in flight is
    /// handed to whoever asks next, and any member can download the result. There is no single
    /// reader to apply contact visibility for, so the archive carries only what every member may
    /// see — contacts shared with the household. A contact kept private, or shared with named
    /// people, stays out, and so does everything hanging off it.
    /// </para>
    /// <para>
    /// Driven by the model rather than a list, so a new table pointing at contacts is covered
    /// without anyone remembering to add it. A required reference to a withheld contact drops the
    /// row — a phone number of a private contact is that contact's data. An optional one is
    /// nulled instead, because the row is somebody else's: a user, a recipe or a vehicle does
    /// not vanish from the archive because it names a private contact.
    /// </para>
    /// </remarks>
    private sealed class ContactScope
    {
        private static readonly ContactScope None = new(new Dictionary<string, string>(), string.Empty);

        private readonly IReadOnlyDictionary<string, string> _nulledColumns;

        private ContactScope(IReadOnlyDictionary<string, string> nulledColumns, string predicate)
        {
            _nulledColumns = nulledColumns;
            Predicate = predicate;
        }

        /// <summary>Appended to the WHERE clause; empty, or starting with " AND".</summary>
        public string Predicate { get; }

        /// <summary>The select-list expression for <paramref name="column"/> of t0.</summary>
        public string Project(string column) =>
            _nulledColumns.TryGetValue(column, out var expression)
                ? expression
                : $"t0.{TenantDataModel.Quote(column)}";

        public static ContactScope For(IEntityType entityType)
        {
            var contact = entityType.Model.FindEntityType(typeof(Contact));
            if (contact == null) return None;

            var shared = SharedLiteral(contact);
            var contactTable = TenantDataModel.Quote(contact);
            var contactKey = TenantDataModel.Quote(
                TenantDataModel.ColumnName(contact, contact.FindPrimaryKey()!.Properties.Single())!);

            string IsShared(string alias) =>
                $"{alias}.{TenantDataModel.Quote(TenantDataModel.ColumnName(contact, contact.FindProperty(nameof(Contact.Visibility))!)!)} = {shared}";

            var predicate = new List<string>();
            var nulled = new Dictionary<string, string>();

            if (entityType == contact)
                predicate.Add(IsShared("t0"));

            foreach (var fk in entityType.GetForeignKeys().Where(fk => fk.PrincipalEntityType == contact))
            {
                var column = TenantDataModel.Quote(
                    TenantDataModel.ColumnName(entityType, fk.Properties.Single())!);
                var lookup = $"FROM {contactTable} c WHERE c.{contactKey} = t0.{column} AND {IsShared("c")}";

                if (fk.IsRequired)
                    predicate.Add($"EXISTS (SELECT 1 {lookup})");
                else
                    nulled[TenantDataModel.ColumnName(entityType, fk.Properties.Single())!] =
                        $"(SELECT c.{contactKey} {lookup})";
            }

            return predicate.Count == 0 && nulled.Count == 0
                ? None
                : new ContactScope(nulled, string.Concat(predicate.Select(p => $" AND {p}")));
        }

        /// <summary>
        /// <see cref="ContactVisibilityLevel.TenantShared"/> as the column stores it. Read through
        /// the model's converter, not assumed, so a change of storage cannot silently match nothing
        /// — or everything.
        /// </summary>
        private static string SharedLiteral(IEntityType contact)
        {
            var property = contact.FindProperty(nameof(Contact.Visibility))!;
            var stored = property.GetTypeMapping().Converter?.ConvertToProvider(ContactVisibilityLevel.TenantShared)
                ?? (int)ContactVisibilityLevel.TenantShared;

            return stored switch
            {
                string text => $"'{text.Replace("'", "''")}'",
                int number => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
                _ => throw new InvalidOperationException(
                    $"Contact visibility is stored as {stored.GetType().Name}, which the export does not know how to compare."),
            };
        }
    }
}
