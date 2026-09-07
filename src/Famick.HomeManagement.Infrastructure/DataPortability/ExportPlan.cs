using Microsoft.EntityFrameworkCore;
using Famick.HomeManagement.Core.DTOs.DataPortability;
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
        var projection = string.Join(", ", columns.Select(c => $"t0.{TenantDataModel.Quote(c.Column)}"));
        var sql = $"SELECT {projection} FROM {TenantDataModel.Quote(entityType)} t0";

        if (tenantPath.Count == 0)
        {
            var tenantColumn = TenantDataModel.TenantColumnName(entityType)
                ?? throw new InvalidOperationException(
                    $"'{entityType.Name}' has no join path to a tenant and no TenantId column of " +
                    "its own, so no household-scoped read of it is possible.");

            return $"{sql} WHERE t0.{TenantDataModel.Quote(tenantColumn)} = {{0}}";
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

        return $"{sql} WHERE t{alias}.{TenantDataModel.Quote(finalTenantColumn)} = {{0}}";
    }
}
