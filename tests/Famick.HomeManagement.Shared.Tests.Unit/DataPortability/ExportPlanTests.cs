using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.DataPortability;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.DataPortability;

/// <summary>
/// Guards the queries an export runs.
/// </summary>
/// <remarks>
/// The failure this is really about is disclosure. A household-scoped read that loses its
/// predicate does not throw — it returns every household's rows and writes them to a file the
/// user downloads. So the predicate is asserted directly, on every table, rather than trusted.
/// </remarks>
public class ExportPlanTests
{
    [Fact]
    public void EveryTableIsReadWithATenantPredicate()
    {
        using var context = RelationalModelContext();

        foreach (var table in ExportPlan.Build(context.Model))
        {
            table.Sql.Should().Contain("WHERE", "{0} must be scoped", table.EntityName);
            table.Sql.Should().Contain("{0}",
                "{0}'s tenant id must be a parameter, never interpolated into the SQL",
                table.EntityName);
        }
    }

    [Fact]
    public void TablesWithoutTheirOwnTenantIdJoinUpToOneThatHasIt()
    {
        using var context = RelationalModelContext();

        var viaParent = ExportPlan.Build(context.Model)
            .Where(t => t.TenantScope == "viaParent")
            .ToList();

        viaParent.Should().NotBeEmpty("the model has children that reach a household through a parent");

        foreach (var table in viaParent)
        {
            table.Sql.Should().Contain("JOIN", "{0}", table.EntityName);
            table.TenantPath.Should().NotBeEmpty("{0}", table.EntityName);
        }
    }

    [Fact]
    public void ExcludedTypesAreAbsentFromThePlanAndExplainedInTheManifest()
    {
        using var context = RelationalModelContext();

        var planned = ExportPlan.Build(context.Model).Select(t => t.EntityName).ToHashSet();

        planned.Should().NotContain(nameof(RefreshToken));
        planned.Should().NotContain(nameof(UserPasskeyCredential));
        planned.Should().NotContain(nameof(HouseholdDataTransfer));

        var exclusions = ExportPlan.Exclusions(context.Model);

        // Stating what is missing, and why, is part of the archive rather than a code comment.
        exclusions.Should().Contain(e => e.Entity == nameof(RefreshToken));
        exclusions.Should().OnlyContain(e => !string.IsNullOrWhiteSpace(e.Reason));
    }

    [Fact]
    public void ExcludedColumnsAreAbsentFromTheProjection()
    {
        using var context = RelationalModelContext();
        var plan = ExportPlan.Build(context.Model);

        var user = plan.Single(t => t.EntityName == nameof(User));
        user.Columns.Should().NotContain(c => c.Property == "PasswordHash");
        user.Sql.Should().NotContain("password_hash");

        var location = plan.Single(t => t.EntityName == nameof(ShoppingLocation));
        location.Columns.Should().NotContain(c => c.Property.StartsWith("OAuth"));
    }

    [Fact]
    public void PlanOrderPlacesParentsBeforeChildren()
    {
        using var context = RelationalModelContext();
        var plan = ExportPlan.Build(context.Model);
        var position = plan.Select((t, i) => (t, i)).ToDictionary(x => x.t.EntityType, x => x.i);

        foreach (var table in plan)
        {
            foreach (var fk in table.EntityType.GetForeignKeys().Where(fk => fk.IsRequired))
            {
                var principal = fk.PrincipalEntityType;
                if (principal == table.EntityType) continue;
                if (!position.ContainsKey(principal)) continue;

                position[principal].Should().BeLessThan(position[table.EntityType],
                    "{0} must be readable before {1} needs it",
                    principal.ClrType.Name, table.EntityName);
            }
        }
    }

    [Fact]
    public void EveryTableGetsItsOwnFileAndOrdinal()
    {
        using var context = RelationalModelContext();
        var plan = ExportPlan.Build(context.Model);

        plan.Select(t => t.FileName).Should().OnlyHaveUniqueItems();
        plan.Select(t => t.Order).Should().OnlyHaveUniqueItems();
        plan.Should().OnlyContain(t => t.Columns.Count > 0);
    }

    private static HomeManagementDbContext RelationalModelContext() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);
}
