using Microsoft.EntityFrameworkCore;
using Famick.HomeManagement.Domain.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Works out, from the EF model alone, which tables hold one household's data and in what
/// order they can be written and deleted.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is derived from <see cref="IModel"/> rather than a hand-written list, because
/// a hand-written list is wrong the first time someone adds an entity and nobody notices. The
/// guard tests over this class are what turn "we forgot to export the new table" from a silent
/// data-loss bug into a red build.
/// </para>
/// <para>
/// The subtlety is that <see cref="ITenantEntity"/> does not cover the household. Thirteen
/// entity types carry no <c>TenantId</c> of their own and reach one only through a required
/// foreign key — <c>MealItem</c> through <c>Meal</c>, <c>BatchCookItem</c> through
/// <c>MealPlanEntry</c> then <c>MealPlan</c>, and so on. The purge gets away with ignoring them
/// because the database cascade-deletes them; an export that ignored them would silently omit
/// real household data. So membership is a fixed point, not a filter.
/// </para>
/// </remarks>
public static class TenantReachability
{
    /// <summary>
    /// Every entity type belonging to a single household: those carrying <c>TenantId</c>
    /// directly, plus everything that reaches one through required foreign keys.
    /// </summary>
    public static IReadOnlyList<IEntityType> TenantScopedEntityTypes(IModel model)
    {
        var reachable = new HashSet<IEntityType>(DirectlyTenantScoped(model));

        // Fixed point: a child of a reachable type is itself reachable, which lets a chain
        // like BatchCookItem -> MealPlanEntry -> MealPlan resolve without assuming hop count.
        bool grew;
        do
        {
            grew = false;
            foreach (var type in MappedEntityTypes(model))
            {
                if (reachable.Contains(type)) continue;

                if (type.GetForeignKeys().Any(fk => fk.IsRequired && reachable.Contains(fk.PrincipalEntityType)))
                {
                    reachable.Add(type);
                    grew = true;
                }
            }
        } while (grew);

        return reachable.OrderBy(t => t.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// The entity types that carry <c>TenantId</c> themselves.
    /// </summary>
    public static IReadOnlyList<IEntityType> DirectlyTenantScoped(IModel model) =>
        MappedEntityTypes(model)
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
            .ToList();

    /// <summary>
    /// The tenant-scoped types that reach a household only through a parent.
    /// </summary>
    public static IReadOnlyList<IEntityType> IndirectlyTenantScoped(IModel model)
    {
        var direct = new HashSet<IEntityType>(DirectlyTenantScoped(model));
        return TenantScopedEntityTypes(model).Where(t => !direct.Contains(t)).ToList();
    }

    /// <summary>
    /// The chain of required foreign keys from <paramref name="entityType"/> up to the nearest
    /// type carrying <c>TenantId</c>, or an empty list when it carries one itself.
    /// </summary>
    /// <remarks>
    /// This is what a tenant-scoped read of an indirectly-scoped table joins through. Shortest
    /// path wins; ties break on the declaring property name so the result is stable across runs
    /// rather than depending on model iteration order.
    /// </remarks>
    public static IReadOnlyList<IForeignKey> TenantJoinPath(IModel model, IEntityType entityType)
    {
        if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
            return Array.Empty<IForeignKey>();

        var scoped = new HashSet<IEntityType>(TenantScopedEntityTypes(model));
        var seen = new HashSet<IEntityType> { entityType };

        // Breadth-first so the first hit is a shortest path.
        var queue = new Queue<(IEntityType Type, List<IForeignKey> Path)>();
        queue.Enqueue((entityType, new List<IForeignKey>()));

        while (queue.Count > 0)
        {
            var (current, path) = queue.Dequeue();

            var candidates = current.GetForeignKeys()
                .Where(fk => fk.IsRequired && scoped.Contains(fk.PrincipalEntityType))
                .OrderBy(fk => string.Join(",", fk.Properties.Select(p => p.Name)), StringComparer.Ordinal);

            foreach (var fk in candidates)
            {
                var next = new List<IForeignKey>(path) { fk };

                if (typeof(ITenantEntity).IsAssignableFrom(fk.PrincipalEntityType.ClrType))
                    return next;

                if (seen.Add(fk.PrincipalEntityType))
                    queue.Enqueue((fk.PrincipalEntityType, next));
            }
        }

        throw new InvalidOperationException(
            $"'{entityType.Name}' was found to be tenant-scoped but no required-FK path to a " +
            "type carrying TenantId could be traced. TenantScopedEntityTypes and TenantJoinPath " +
            "have diverged; fix them together.");
    }

    private static IEnumerable<IEntityType> MappedEntityTypes(IModel model) =>
        // Owned and keyless types have no table of their own, so they travel with their owner.
        model.GetEntityTypes().Where(t => t.GetTableName() != null);
}
