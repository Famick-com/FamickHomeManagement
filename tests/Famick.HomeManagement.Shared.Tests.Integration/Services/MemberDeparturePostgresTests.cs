using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Issue #99: removing a member who has used the app. Against Postgres because the failure is a
/// foreign key — the in-memory provider does not enforce <c>Restrict</c>, which is how deleting
/// any real member came to fail without a test noticing.
/// </summary>
public class MemberDeparturePostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task AnAdminCanRemoveAMemberWhoCreatedContactsEventsAndStock()
    {
        var seed = await SeedAsync();

        await using (var db = fixture.CreateDbContext())
        {
            await UserManagement(db, seed.TenantId).DeleteUserAsync(seed.MemberId, seed.AdminId);
        }

        await AssertDepartedAsync(seed, successorId: seed.AdminId);
    }

    [Fact]
    public async Task AMembersOwnDeletionCompletesWhenItComesDue()
    {
        var seed = await SeedAsync();

        await using (var db = fixture.CreateDbContext())
        {
            var member = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == seed.MemberId);
            member.DeletionRequestedAt = DateTime.UtcNow.AddDays(-31);
            member.DeletionPurgeAfter = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var deletion = new AccountDeletionService(
                db, Mock.Of<IJwtMinIatService>(), NullLogger<AccountDeletionService>.Instance);

            var summary = await deletion.PurgeDueAsync(DateTime.UtcNow);
            summary.UsersPurged.Should().BeGreaterThanOrEqualTo(1);
        }

        // Nobody is removing them, so the household's Admin inherits what was shared.
        await AssertDepartedAsync(seed, successorId: seed.AdminId);
    }

    private async Task AssertDepartedAsync(Seed seed, Guid successorId)
    {
        await using var db = fixture.CreateDbContext();

        (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == seed.MemberId)).Should().BeFalse();

        var contacts = await db.Contacts.IgnoreQueryFilters()
            .Where(c => c.TenantId == seed.TenantId)
            .ToDictionaryAsync(c => c.Id);

        // What only the member could see goes with them.
        contacts.Should().NotContainKey(seed.PrivateContactId);
        contacts.Should().NotContainKey(seed.NamedShareContactId);
        contacts.Should().NotContainKey(seed.PrivateGroupId);
        (await db.ContactPhoneNumbers.IgnoreQueryFilters().AnyAsync(p => p.ContactId == seed.PrivateContactId))
            .Should().BeFalse();

        // What the household could see stays, under someone who can still edit it.
        contacts[seed.MemberCardId].CreatedByUserId.Should().Be(successorId);
        contacts[seed.MemberCardId].LinkedUserId.Should().BeNull();

        // A household contact filed under the member's private group is not lost with the group.
        contacts[seed.SharedChildId].ParentContactId.Should().Be(seed.HouseholdGroupId);

        (await db.CalendarEvents.IgnoreQueryFilters().SingleAsync(e => e.Id == seed.EventId))
            .CreatedByUserId.Should().Be(successorId);

        // History keeps the row and stops naming the member, rather than blaming someone else.
        (await db.StockLog.IgnoreQueryFilters().SingleAsync(l => l.Id == seed.StockLogId))
            .UserId.Should().BeNull();
        (await db.ContactAuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Id == seed.AuditLogId))
            .UserId.Should().BeNull();
    }

    private static UserManagementService UserManagement(HomeManagementDbContext db, Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(p => p.TenantId).Returns(tenantId);

        return new UserManagementService(
            db,
            Mock.Of<IPasswordHasher>(),
            tenantProvider.Object,
            Mock.Of<IHouseholdInvitationService>(),
            new HouseholdMemberFactory(db, Mock.Of<IContactService>(), NullLogger<HouseholdMemberFactory>.Instance),
            NullLogger<UserManagementService>.Instance);
    }

    private sealed record Seed(
        Guid TenantId, Guid AdminId, Guid MemberId,
        Guid HouseholdGroupId, Guid MemberCardId, Guid PrivateContactId, Guid NamedShareContactId,
        Guid PrivateGroupId, Guid SharedChildId, Guid EventId, Guid StockLogId, Guid AuditLogId);

    private async Task<Seed> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await using var db = fixture.CreateDbContext();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Household" });
        db.Users.AddRange(NewUser(tenantId, adminId, DateTime.UtcNow.AddYears(-1)), NewUser(tenantId, memberId, DateTime.UtcNow));
        db.UserRoles.AddRange(
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = adminId, Role = Role.Admin },
            new UserRole { Id = Guid.NewGuid(), TenantId = tenantId, UserId = memberId, Role = Role.Editor });
        await db.SaveChangesAsync();

        var household = NewContact(tenantId, adminId, ContactVisibilityLevel.TenantShared, "Household");
        household.IsTenantHousehold = true;

        var memberCard = NewContact(tenantId, memberId, ContactVisibilityLevel.TenantShared, "Member", household.Id);
        memberCard.LinkedUserId = memberId;

        var privateContact = NewContact(tenantId, memberId, ContactVisibilityLevel.UserPrivate, "Secret", household.Id);
        var namedShare = NewContact(tenantId, memberId, ContactVisibilityLevel.SharedWithUsers, "Named", household.Id);
        var privateGroup = NewContact(tenantId, memberId, ContactVisibilityLevel.UserPrivate, "Private Group");
        var sharedChild = NewContact(tenantId, adminId, ContactVisibilityLevel.TenantShared, "Child", privateGroup.Id);

        db.Contacts.Add(household);
        await db.SaveChangesAsync();
        db.Contacts.Add(privateGroup);
        await db.SaveChangesAsync();
        db.Contacts.AddRange(memberCard, privateContact, namedShare, sharedChild);
        await db.SaveChangesAsync();

        db.ContactPhoneNumbers.Add(new ContactPhoneNumber
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ContactId = privateContact.Id, PhoneNumber = "555-0100",
        });
        db.ContactUserShares.Add(new ContactUserShare
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ContactId = namedShare.Id, SharedWithUserId = adminId,
        });

        var auditLog = new ContactAuditLog
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ContactId = memberCard.Id, UserId = memberId,
            Action = ContactAuditAction.Created,
        };
        db.ContactAuditLogs.Add(auditLog);

        var calendarEvent = new CalendarEvent
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Title = "Dentist", CreatedByUserId = memberId,
            StartTimeUtc = DateTime.UtcNow, EndTimeUtc = DateTime.UtcNow.AddHours(1),
        };
        db.CalendarEvents.Add(calendarEvent);

        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" };
        var unit = new QuantityUnit { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Piece", NamePlural = "Pieces" };
        var product = new Product
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Name = "Rice", LocationId = location.Id,
            QuantityUnitIdPurchase = unit.Id, QuantityUnitIdStock = unit.Id,
        };
        db.Locations.Add(location);
        db.QuantityUnits.Add(unit);
        db.Products.Add(product);

        var stockLog = new StockLog
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id, Amount = 1,
            StockId = Guid.NewGuid().ToString(), TransactionType = "purchase", UserId = memberId,
        };
        db.StockLog.Add(stockLog);

        await db.SaveChangesAsync();

        return new Seed(
            tenantId, adminId, memberId, household.Id, memberCard.Id, privateContact.Id, namedShare.Id,
            privateGroup.Id, sharedChild.Id, calendarEvent.Id, stockLog.Id, auditLog.Id);
    }

    private static User NewUser(Guid tenantId, Guid id, DateTime createdAt) => new()
    {
        Id = id, TenantId = tenantId, Username = $"u-{id:N}", Email = $"{id:N}@example.com",
        FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true, CreatedAt = createdAt,
    };

    private static Contact NewContact(
        Guid tenantId, Guid createdBy, ContactVisibilityLevel visibility, string name, Guid? parentId = null) => new()
    {
        Id = Guid.NewGuid(), TenantId = tenantId, FirstName = name, CreatedByUserId = createdBy,
        Visibility = visibility, ParentContactId = parentId,
    };
}
