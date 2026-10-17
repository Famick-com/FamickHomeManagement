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
            // The column name, not merely the presence of a WHERE. A predicate on the wrong
            // column would satisfy "contains WHERE" while returning every household's rows.
            var expectedColumn = table.TenantScope == "own"
                ? TenantDataModel.TenantColumnName(table.EntityType)
                : TenantDataModel.TenantColumnName(
                    TenantReachability.TenantJoinPath(context.Model, table.EntityType)[^1].PrincipalEntityType);

            expectedColumn.Should().NotBeNull("{0} must reach a TenantId somewhere", table.EntityName);

            table.Sql.Should().MatchRegex($@"WHERE\s+t\d+\.""{expectedColumn}""\s*=",
                "{0} must be filtered on {1}", table.EntityName, expectedColumn);

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

    [Fact]
    public void ContactsAreExportedOnlyWhenSharedWithTheHousehold()
    {
        using var context = RelationalModelContext();
        var plan = ExportPlan.Build(context.Model);

        // Any member can download a household archive, so it holds only what every member
        // may see.
        plan.Single(t => t.EntityName == nameof(Contact)).Sql
            .Should().Contain(@"t0.""Visibility"" = 'TenantShared'");
    }

    [Fact]
    public void RowsThatBelongToAContactFollowItsVisibility()
    {
        using var context = RelationalModelContext();
        var contact = context.Model.FindEntityType(typeof(Contact))!;
        var plan = ExportPlan.Build(context.Model);

        var dependents = plan
            .Where(t => t.EntityType.GetForeignKeys().Any(fk => fk.IsRequired && fk.PrincipalEntityType == contact))
            .ToList();

        dependents.Select(t => t.EntityName).Should().Contain(
            [nameof(ContactPhoneNumber), nameof(ContactEmailAddress), nameof(ContactAddress), nameof(ContactAuditLog)]);

        foreach (var table in dependents)
            table.Sql.Should().Contain("EXISTS (SELECT 1 FROM", "{0} belongs to a contact", table.EntityName)
                .And.Contain(@"c.""Visibility"" = 'TenantShared'", "{0}", table.EntityName);
    }

    [Fact]
    public void OptionalReferencesToAContactAreNulledRatherThanDroppingTheRow()
    {
        using var context = RelationalModelContext();
        var user = ExportPlan.Build(context.Model).Single(t => t.EntityName == nameof(User));

        // A user is not a contact's data; naming a private contact must not drop the user.
        user.Sql.Should().NotContain("EXISTS");
        user.Sql.Should().Contain(@"(SELECT c.""Id"" FROM ""contacts"" c WHERE c.""Id"" = t0.""ContactId""");
    }

    private static HomeManagementDbContext RelationalModelContext() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);
}
