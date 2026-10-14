using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Issue #119 against real Postgres: the unique hash index, and the race it settles, exist only
/// there. The behaviour itself is covered more fully by the unit tests in
/// <c>SharedAddressIsolationTests</c>.
/// </summary>
public class SharedAddressIsolationPostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    private HomeManagementDbContext ContextFor(Guid? tenantId, IInterceptor? interceptor = null)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        var builder = new DbContextOptionsBuilder<HomeManagementDbContext>().UseNpgsql(fixture.ConnectionString);
        if (interceptor != null)
            builder.AddInterceptors(interceptor);
        return new HomeManagementDbContext(builder.Options, tenantProvider.Object);
    }

    private static ContactService ContactService(HomeManagementDbContext db, Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(tenantId);
        return new ContactService(db, tenantProvider.Object, Mock.Of<IFileStorageService>(), Mock.Of<IFileUrlService>(),
            new AddressHasher(new PassThroughAddressCanonicalizer()), NullLogger<ContactService>.Instance);
    }

    /// <summary>A household with one user, since every contact records who created it.</summary>
    private static Guid AddHousehold(HomeManagementDbContext db, Guid tenantId, string name)
    {
        var userId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = name });
        db.Users.Add(new User
        {
            Id = userId, TenantId = tenantId, Username = $"u-{userId:N}", Email = $"{userId:N}@example.com",
            FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true
        });
        return userId;
    }

    /// <summary>Two households whose contacts point at one Address row, as rows came to be shared before the fix.</summary>
    private async Task<(Guid victim, Guid attacker, Guid attackerContact, Guid attackerLink, Guid shared)> SeedSharedRowAsync()
    {
        await using var db = ContextFor(null);
        var victim = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var shared = new Address
        {
            Id = Guid.NewGuid(), AddressLine1 = "123 Main St", City = "Springfield", StateProvince = "IL",
            PostalCode = "62701", Country = "USA", NormalizedHash = $"hash-{Guid.NewGuid():N}"
        };
        var victimUser = AddHousehold(db, victim, "Victim");
        var attackerUser = AddHousehold(db, attacker, "Attacker");
        db.Addresses.Add(shared);
        var victimContact = new Contact { Id = Guid.NewGuid(), TenantId = victim, FirstName = "V", IsActive = true, CreatedByUserId = victimUser };
        var attackerContact = new Contact { Id = Guid.NewGuid(), TenantId = attacker, FirstName = "A", IsActive = true, CreatedByUserId = attackerUser };
        db.Contacts.AddRange(victimContact, attackerContact);
        var attackerLink = new ContactAddress
        {
            Id = Guid.NewGuid(), TenantId = attacker, ContactId = attackerContact.Id, AddressId = shared.Id, IsPrimary = true
        };
        db.ContactAddresses.AddRange(
            new ContactAddress { Id = Guid.NewGuid(), TenantId = victim, ContactId = victimContact.Id, AddressId = shared.Id, IsPrimary = true },
            attackerLink);
        await db.SaveChangesAsync();
        return (victim, attacker, attackerContact.Id, attackerLink.Id, shared.Id);
    }

    [Fact]
    public async Task Editing_a_shared_row_through_one_household_leaves_the_other_untouched()
    {
        var seed = await SeedSharedRowAsync();

        await using (var db = ContextFor(seed.attacker))
        {
            var result = await ContactService(db, seed.attacker).UpdateAddressAsync(seed.attackerContact, seed.attackerLink,
                new AddContactAddressRequest
                {
                    AddressLine1 = "666 Elsewhere Rd", City = "Springfield", StateProvince = "IL",
                    PostalCode = "62701", Country = "USA", IsPrimary = true
                });
            result.AddressId.Should().NotBe(seed.shared);
        }

        await using var check = ContextFor(null);
        (await check.Addresses.SingleAsync(a => a.Id == seed.shared)).AddressLine1.Should().Be("123 Main St");
    }

    [Fact]
    public async Task Two_households_saving_the_same_address_at_once_end_up_on_one_row()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        Guid firstContact, secondContact;
        await using (var setup = ContextFor(null))
        {
            var firstUser = AddHousehold(setup, first, "First");
            var secondUser = AddHousehold(setup, second, "Second");
            firstContact = Guid.NewGuid();
            secondContact = Guid.NewGuid();
            setup.Contacts.AddRange(
                new Contact { Id = firstContact, TenantId = first, FirstName = "F", IsActive = true, CreatedByUserId = firstUser },
                new Contact { Id = secondContact, TenantId = second, FirstName = "S", IsActive = true, CreatedByUserId = secondUser });
            await setup.SaveChangesAsync();
        }

        var line1 = $"{Guid.NewGuid():N} Race Street";
        AddContactAddressRequest Request() => new()
        {
            AddressLine1 = line1, City = "Springfield", StateProvince = "IL", PostalCode = "62701", Country = "USA",
            Tag = AddressTag.Home, IsPrimary = true
        };

        // The second household inserts the same address after the first has looked for it and
        // is about to insert its own, so the first insert hits the unique index.
        var rival = new BeforeFirstAddressInsert(async () =>
        {
            await using var rivalDb = ContextFor(second);
            await ContactService(rivalDb, second).AddAddressAsync(secondContact, Request());
        });
        ContactAddressDto firstLink;
        await using (var db = ContextFor(first, rival))
        {
            firstLink = await ContactService(db, first).AddAddressAsync(firstContact, Request());
        }

        rival.Fired.Should().BeTrue();
        await using var check = ContextFor(null);
        var rows = await check.Addresses.Where(a => a.AddressLine1 == line1).ToListAsync();
        rows.Should().ContainSingle();
        firstLink.AddressId.Should().Be(rows[0].Id);
        (await check.ContactAddresses.IgnoreQueryFilters().SingleAsync(ca => ca.ContactId == secondContact))
            .AddressId.Should().Be(rows[0].Id);
    }

    [Fact]
    public async Task The_database_refuses_a_second_row_for_the_same_address()
    {
        var hash = $"hash-{Guid.NewGuid():N}";
        await using (var db = ContextFor(null))
        {
            db.Addresses.Add(new Address { Id = Guid.NewGuid(), AddressLine1 = "1 Once St", NormalizedHash = hash });
            await db.SaveChangesAsync();
        }

        await using var again = ContextFor(null);
        again.Addresses.Add(new Address { Id = Guid.NewGuid(), AddressLine1 = "1 Once St", NormalizedHash = hash });
        var act = () => again.SaveChangesAsync();

        (await act.Should().ThrowAsync<DbUpdateException>())
            .WithInnerException<PostgresException>()
            .Which.ConstraintName.Should().Be(AddressRegistry.HashIndexName);
    }

    /// <summary>Runs a rival write once, just before the context first saves a new Address.</summary>
    private sealed class BeforeFirstAddressInsert(Func<Task> rival) : SaveChangesInterceptor
    {
        public bool Fired { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Fired && eventData.Context!.ChangeTracker.Entries<Address>().Any(e => e.State == EntityState.Added))
            {
                Fired = true;
                await rival();
            }
            return result;
        }
    }
}
