using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.DataPortability;

/// <summary>
/// Guards the model walk that decides what belongs to a household.
/// </summary>
/// <remarks>
/// These tests exist because the failure they prevent is silent. An entity added without a
/// TenantId and without a query filter is invisible to the purge, invisible to an export, and
/// produces no error anywhere — you find out when a household's data turns up in someone else's
/// archive, or when their meal plans quietly fail to come back from a backup. A red build is a
/// much better place to learn it.
/// </remarks>
public class TenantReachabilityTests
{
    /// <summary>
    /// The child entities that reach a household through a parent rather than carrying TenantId.
    /// Mirrors HomeManagementDbContext.ApplyChildEntityQueryFilters.
    /// </summary>
    private static readonly Type[] ParentFilteredChildren =
    [
        typeof(CalendarEventException),
        typeof(CalendarEventMember),
        typeof(ExternalCalendarEvent),
        typeof(MealItem),
        typeof(MealPlanEntry),
        typeof(BatchCookItem),
        typeof(BatchCookItemUsage),
        typeof(ContactAllergen),
        typeof(ContactDietaryPreference),
        typeof(ProductAllergen),
        typeof(ProductDietaryConflict),
    ];

    [Fact]
    public void ReachableSetIncludesEveryParentFilteredChild()
    {
        using var context = RelationalModelContext();
        var reachable = TenantReachability.TenantScopedEntityTypes(context.Model)
            .Select(t => t.ClrType)
            .ToHashSet();

        var missing = ParentFilteredChildren.Where(t => !reachable.Contains(t)).ToList();

        missing.Should().BeEmpty(
            "every entity filtered through a parent in ApplyChildEntityQueryFilters holds household " +
            "data and must be reachable, or an export silently omits it. Missing: {0}",
            string.Join(", ", missing.Select(t => t.Name)));
    }

    [Fact]
    public void ReachableSetIsAStrictSupersetOfTheHandWrittenFilterList()
    {
        using var context = RelationalModelContext();

        var indirect = TenantReachability.IndirectlyTenantScoped(context.Model)
            .Select(t => t.ClrType)
            .ToHashSet();

        // The point of computing reachability rather than listing it: these two carry a bare
        // UserId, are not in ApplyChildEntityQueryFilters, and therefore have no query filter at
        // all. A hand-maintained list would not have them.
        indirect.Should().Contain(typeof(UserMealPlannerPreference));
        indirect.Should().Contain(typeof(UserMealPlannerTip));

        indirect.Should().HaveCountGreaterThan(ParentFilteredChildren.Length,
            "the computed walk should find more than the hand-written filter list, not fewer");
    }

    [Fact]
    public void DirectlyScopedTypesAreExactlyTheTenantEntities()
    {
        using var context = RelationalModelContext();

        TenantReachability.DirectlyTenantScoped(context.Model)
            .Should().OnlyContain(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType));
    }

    [Fact]
    public void EveryIndirectlyScopedTypeHasATraceableJoinPathToATenant()
    {
        using var context = RelationalModelContext();

        foreach (var type in TenantReachability.IndirectlyTenantScoped(context.Model))
        {
            var path = TenantReachability.TenantJoinPath(context.Model, type);

            path.Should().NotBeEmpty("{0} is tenant-scoped but has no route to a TenantId", type.Name);
            typeof(ITenantEntity).IsAssignableFrom(path[^1].PrincipalEntityType.ClrType)
                .Should().BeTrue("the last hop of {0}'s join path must land on a TenantId", type.Name);
        }
    }

    [Fact]
    public void JoinPathForADirectlyScopedTypeIsEmpty()
    {
        using var context = RelationalModelContext();
        var product = context.Model.FindEntityType(typeof(Product))!;

        TenantReachability.TenantJoinPath(context.Model, product).Should().BeEmpty();
    }

    [Fact]
    public void MultiHopChildrenResolveThroughEveryIntermediateTable()
    {
        using var context = RelationalModelContext();
        var usage = context.Model.FindEntityType(typeof(BatchCookItemUsage))!;

        // BatchCookItemUsage -> BatchCookItem -> Product. Asserting more than one hop is the
        // point: a single-hop assumption would leave this table unscoped and silently omitted.
        var path = TenantReachability.TenantJoinPath(context.Model, usage);

        path.Should().HaveCount(2);
        path[^1].PrincipalEntityType.ClrType.Should().Be(typeof(Product));
    }

    [Fact]
    public void JoinPathTakesTheShortestRouteAvailable()
    {
        using var context = RelationalModelContext();
        var batchCookItem = context.Model.FindEntityType(typeof(BatchCookItem))!;

        // BatchCookItem can reach a household two ways: SourceEntryId -> MealPlanEntry ->
        // MealPlan, or ProductId -> Product directly. Both give the same tenant, because a batch
        // cook item's product and its meal plan belong to the same household, so the shorter
        // join wins. Recorded here because the route is not obvious from the query filter in
        // ApplyChildEntityQueryFilters, which navigates the longer one.
        var path = TenantReachability.TenantJoinPath(context.Model, batchCookItem);

        path.Should().ContainSingle();
        path[0].PrincipalEntityType.ClrType.Should().Be(typeof(Product));
    }

    [Fact]
    public void GloballySharedReferenceDataIsNotTenantScoped()
    {
        using var context = RelationalModelContext();
        var reachable = TenantReachability.TenantScopedEntityTypes(context.Model)
            .Select(t => t.ClrType)
            .ToHashSet();

        // Licensed catalog and seeded reference data belong to the deployment, not a household.
        // If reachability ever drags these in, an export starts redistributing them.
        reachable.Should().NotContain(typeof(MasterProduct));
        reachable.Should().NotContain(typeof(MasterProductBarcode));
        reachable.Should().NotContain(typeof(MasterProductImage));
        reachable.Should().NotContain(typeof(MasterProductNutrition));
        reachable.Should().NotContain(typeof(Permission));
    }

    private static HomeManagementDbContext RelationalModelContext()
    {
        // Npgsql, but never connected: the InMemory provider carries no relational metadata, so
        // table and column names would come back null.
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only")
                .Options);
    }
}
