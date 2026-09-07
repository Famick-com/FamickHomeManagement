using Microsoft.EntityFrameworkCore;
using Famick.HomeManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Ordering and SQL-shape helpers for whole-household operations over the EF model.
/// </summary>
/// <remarks>
/// Purging a household and restoring one are the same graph walked in opposite directions, so
/// they share this code rather than each carrying their own copy. When they drift, the failure
/// is a foreign-key violation halfway through a destructive operation, which is a bad place to
/// discover a disagreement.
/// </remarks>
public static class TenantDataModel
{
    /// <summary>
    /// Orders the entity types carrying <c>TenantId</c> so that every type comes before the
    /// types it points at — dependents first, principals last.
    /// </summary>
    /// <remarks>
    /// Only required relationships constrain the order. Optional ones are nulled first (see
    /// <see cref="OptionalReferenceColumns"/>), which is what makes an order possible at all:
    /// <c>User.ContactId</c> and <c>Contact.LinkedUserId</c> point at each other, so the graph
    /// genuinely has a cycle until the nullable edges are cut. A cycle among required
    /// relationships could not be ordered — but could not be inserted either, so it cannot arise.
    /// </remarks>
    public static IReadOnlyList<IEntityType> EntityTypesInDeleteOrder(IModel model)
    {
        var tenantTypes = TenantReachability.DirectlyTenantScoped(model);

        var inScope = new HashSet<IEntityType>(tenantTypes);
        var ordered = new List<IEntityType>();
        var visiting = new HashSet<IEntityType>();
        var visited = new HashSet<IEntityType>();

        void Visit(IEntityType type)
        {
            if (visited.Contains(type) || !visiting.Add(type)) return;

            // Everything that requires this type has to go first.
            foreach (var fk in type.GetReferencingForeignKeys())
            {
                var dependent = fk.DeclaringEntityType;
                if (dependent != type && fk.IsRequired && inScope.Contains(dependent))
                    Visit(dependent);
            }

            visiting.Remove(type);
            visited.Add(type);
            ordered.Add(type);
        }

        foreach (var type in tenantTypes) Visit(type);

        return ordered;
    }

    /// <summary>
    /// Orders every tenant-scoped type — including the ones reaching a household only through a
    /// parent — so that a type comes after everything it points at. The order rows can be written in.
    /// </summary>
    /// <remarks>
    /// Not simply <see cref="EntityTypesInDeleteOrder"/> reversed: the delete order covers only
    /// the types carrying <c>TenantId</c>, because the database cascade handles the rest. A write
    /// has no cascade to lean on and must place every table itself.
    /// </remarks>
    public static IReadOnlyList<IEntityType> EntityTypesInWriteOrder(IModel model)
    {
        var scoped = TenantReachability.TenantScopedEntityTypes(model);

        var inScope = new HashSet<IEntityType>(scoped);
        var ordered = new List<IEntityType>();
        var visiting = new HashSet<IEntityType>();
        var visited = new HashSet<IEntityType>();

        void Visit(IEntityType type)
        {
            if (visited.Contains(type) || !visiting.Add(type)) return;

            // Everything this type requires has to exist first.
            foreach (var fk in type.GetForeignKeys())
            {
                var principal = fk.PrincipalEntityType;
                if (principal != type && fk.IsRequired && inScope.Contains(principal))
                    Visit(principal);
            }

            visiting.Remove(type);
            visited.Add(type);
            ordered.Add(type);
        }

        foreach (var type in scoped) Visit(type);

        return ordered;
    }

    /// <summary>
    /// The nullable foreign-key columns on <paramref name="entityType"/> that point at another
    /// type in <paramref name="inScope"/> — the edges a purge cuts before deleting and a restore
    /// leaves null until a second pass.
    /// </summary>
    /// <remarks>
    /// Two things need this. Cycles: <c>User</c> and <c>Contact</c> reference each other, so
    /// neither can go first while both links stand. And self-referencing hierarchies —
    /// <c>Contact.ParentContactId</c> — where a Restrict rule is checked per row and refuses a
    /// parent whose children are in the same statement.
    /// </remarks>
    public static IReadOnlyList<string> OptionalReferenceColumns(
        IEntityType entityType,
        ISet<IEntityType> inScope)
    {
        var tenantColumn = TenantColumnName(entityType);

        return entityType.GetForeignKeys()
            .Where(fk => !fk.IsRequired)
            .Where(fk => inScope.Contains(fk.PrincipalEntityType))
            .SelectMany(fk => fk.Properties)
            .Where(p => p.IsNullable)
            .Select(p => ColumnName(entityType, p))
            .Where(c => c != null && c != tenantColumn)
            .Select(c => c!)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The physical column holding <see cref="ITenantEntity.TenantId"/>, or null when the type
    /// reaches its household through a parent instead.
    /// </summary>
    public static string? TenantColumnName(IEntityType entityType)
    {
        var property = entityType.FindProperty(nameof(ITenantEntity.TenantId));
        return property == null ? null : ColumnName(entityType, property);
    }

    public static string? ColumnName(IEntityType entityType, IProperty property)
    {
        var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
        return storeObject.HasValue
            ? property.GetColumnName(storeObject.Value)
            : property.GetColumnName();
    }

    public static string Quote(IEntityType entityType)
    {
        var schema = entityType.GetSchema();
        var table = entityType.GetTableName()!;
        return schema == null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
    }

    public static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";
}
