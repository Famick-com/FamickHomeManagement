using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.DataPortability;

/// <summary>
/// Guards the orderings a household-wide write or delete depends on.
/// </summary>
/// <remarks>
/// A wrong order here surfaces as a foreign-key violation halfway through a destructive
/// operation, which is the worst possible place to find out. The model changes often enough that
/// asserting the invariant beats reviewing the order by eye.
/// </remarks>
public class TenantDataModelTests
{
    [Fact]
    public void DeleteOrderPlacesEveryDependentBeforeWhatItRequires()
    {
        using var context = RelationalModelContext();
        var order = TenantDataModel.EntityTypesInDeleteOrder(context.Model);
        var position = order.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

        var violations = new List<string>();

        foreach (var dependent in order)
        {
            foreach (var fk in dependent.GetForeignKeys().Where(fk => fk.IsRequired))
            {
                var principal = fk.PrincipalEntityType;
                if (principal == dependent) continue;            // self-reference, same table
                if (!position.ContainsKey(principal)) continue;   // points outside the set

                if (position[dependent] > position[principal])
                    violations.Add($"{dependent.ClrType.Name} -> {principal.ClrType.Name}");
            }
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void WriteOrderPlacesEveryRequiredPrincipalBeforeItsDependents()
    {
        using var context = RelationalModelContext();
        var order = TenantDataModel.EntityTypesInWriteOrder(context.Model);
        var position = order.Select((t, i) => (t, i)).ToDictionary(x => x.t, x => x.i);

        var violations = new List<string>();

        foreach (var dependent in order)
        {
            foreach (var fk in dependent.GetForeignKeys().Where(fk => fk.IsRequired))
            {
                var principal = fk.PrincipalEntityType;
                if (principal == dependent) continue;
                if (!position.ContainsKey(principal)) continue;

                if (position[principal] > position[dependent])
                    violations.Add($"{principal.ClrType.Name} written after {dependent.ClrType.Name}");
            }
        }

        violations.Should().BeEmpty();
    }

    [Fact]
    public void WriteOrderCoversEveryTenantScopedTypeIncludingTheParentFilteredChildren()
    {
        using var context = RelationalModelContext();

        // The delete order can lean on the database cascade for children that carry no TenantId.
        // A write has no cascade to lean on, so it must place every table itself — which is why
        // these two orderings are not each other's reverse.
        TenantDataModel.EntityTypesInWriteOrder(context.Model)
            .Should().BeEquivalentTo(TenantReachability.TenantScopedEntityTypes(context.Model));

        TenantDataModel.EntityTypesInWriteOrder(context.Model).Count
            .Should().BeGreaterThan(TenantDataModel.EntityTypesInDeleteOrder(context.Model).Count);
    }

    [Fact]
    public void AccountDeletionStillDelegatesToTheSharedOrdering()
    {
        using var context = RelationalModelContext();

        // The refactor that extracted this must not have changed what the purge does. When these
        // two disagree, a household is deleted in an order nothing tested.
        AccountDeletionService.TenantEntityTypesInDeleteOrder(context.Model)
            .Should().Equal(TenantDataModel.EntityTypesInDeleteOrder(context.Model));
    }

    [Fact]
    public void OptionalReferenceColumnsFindTheEdgesThatMakeTheGraphAcyclic()
    {
        using var context = RelationalModelContext();
        var inScope = TenantReachability.DirectlyTenantScoped(context.Model).ToHashSet();

        var contact = context.Model.FindEntityType(typeof(Contact))!;
        var user = context.Model.FindEntityType(typeof(User))!;

        // The self-referencing hierarchy: a Restrict rule is checked per row and refuses a
        // parent whose children are in the same statement.
        TenantDataModel.OptionalReferenceColumns(contact, inScope)
            .Should().Contain("ParentContactId");

        // User -> Contact is a real, configured relationship (UserConfiguration.cs:83).
        TenantDataModel.OptionalReferenceColumns(user, inScope)
            .Should().Contain("ContactId");
    }

    [Fact]
    public void ContactLinkedUserIdIsNotAModelledForeignKey()
    {
        using var context = RelationalModelContext();
        var contact = context.Model.FindEntityType(typeof(Contact))!;
        var inScope = TenantReachability.DirectlyTenantScoped(context.Model).ToHashSet();

        // Contact.LinkedUserId is a bare nullable Guid with no HasOne and no navigation, so EF
        // does not know it points at User. Nothing model-driven can see it: not this method, not
        // the FK-cycle pass, not any future reference rewriting. Recorded as a test because the
        // doc comment on AccountDeletionService.TenantEntityTypesInDeleteOrder calls it half of a
        // cycle with User.ContactId, and it is not one — the only modelled edge is User -> Contact.
        //
        // Harmless for a same-household restore, where user ids are preserved and the value stays
        // valid. It would need explicit handling for anything that remaps user ids.
        contact.GetForeignKeys()
            .SelectMany(fk => fk.Properties)
            .Should().NotContain(p => p.Name == "LinkedUserId");

        TenantDataModel.OptionalReferenceColumns(contact, inScope)
            .Should().NotContain("LinkedUserId");
    }

    [Fact]
    public void OptionalReferenceColumnsNeverIncludeTheTenantColumn()
    {
        using var context = RelationalModelContext();
        var inScope = TenantReachability.DirectlyTenantScoped(context.Model).ToHashSet();

        foreach (var type in inScope)
        {
            var tenantColumn = TenantDataModel.TenantColumnName(type);
            if (tenantColumn == null) continue;

            // Nulling TenantId would orphan the row from the household mid-purge, and on a
            // restore it would write a row nobody can query.
            TenantDataModel.OptionalReferenceColumns(type, inScope)
                .Should().NotContain(tenantColumn, "{0}", type.ClrType.Name);
        }
    }

    [Fact]
    public void QuoteEscapesEmbeddedQuotes()
    {
        TenantDataModel.Quote("odd\"name").Should().Be("\"odd\"\"name\"");
    }

    private static HomeManagementDbContext RelationalModelContext() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);
}
