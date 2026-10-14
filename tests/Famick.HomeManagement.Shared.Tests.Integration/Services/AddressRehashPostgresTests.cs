using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Issue #119: the address hash is unique, so turning on a canonicalizer and rehashing can
/// make two rows the same address. The rehash merges them instead of failing. Its own
/// container, because a rehash walks every row in the table.
/// </summary>
public class AddressRehashPostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task A_rehash_that_makes_two_rows_one_address_merges_them()
    {
        var passThrough = new AddressHasher(new PassThroughAddressCanonicalizer());
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var shortForm = await NewAddressAsync(passThrough, "1 Main St");
        var longForm = await NewAddressAsync(passThrough, "1 Main Street");
        var onlyLong = Guid.NewGuid();
        var both = Guid.NewGuid();
        Guid longLinkOfOnlyLong;
        await using (var db = fixture.CreateDbContext())
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "Household", AddressId = longForm.Id, AddressLine2 = "Apt 1" });
            db.Users.Add(new User
            {
                Id = userId, TenantId = tenantId, Username = $"u-{userId:N}", Email = $"{userId:N}@example.com",
                FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true
            });
            db.Contacts.AddRange(
                new Contact { Id = onlyLong, TenantId = tenantId, FirstName = "L", IsActive = true, CreatedByUserId = userId },
                new Contact { Id = both, TenantId = tenantId, FirstName = "B", IsActive = true, CreatedByUserId = userId });
            longLinkOfOnlyLong = Guid.NewGuid();
            db.ContactAddresses.AddRange(
                new ContactAddress { Id = longLinkOfOnlyLong, TenantId = tenantId, ContactId = onlyLong, AddressId = longForm.Id, IsPrimary = true },
                new ContactAddress { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = both, AddressId = shortForm.Id, IsPrimary = true },
                new ContactAddress { Id = Guid.NewGuid(), TenantId = tenantId, ContactId = both, AddressId = longForm.Id });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateDbContext())
        {
            var service = new AddressService(db, Mock.Of<ITenantProvider>(), Mock.Of<IAddressAutocompleteProvider>(),
                Mock.Of<IAddressSuggestionCache>(), new AddressHasher(new StreetToSt()), NullLogger<AddressService>.Instance);
            var result = await service.RehashAddressesAsync(100, null);
            result.HasMore.Should().BeFalse();
        }

        await using var check = fixture.CreateDbContext();
        (await check.Addresses.AnyAsync(a => a.Id == longForm.Id)).Should().BeFalse();
        (await check.Addresses.AnyAsync(a => a.Id == shortForm.Id)).Should().BeTrue();
        var tenant = await check.Tenants.SingleAsync(t => t.Id == tenantId);
        tenant.AddressId.Should().Be(shortForm.Id);
        tenant.AddressLine2.Should().Be("Apt 1");
        (await check.ContactAddresses.IgnoreQueryFilters().SingleAsync(ca => ca.Id == longLinkOfOnlyLong))
            .AddressId.Should().Be(shortForm.Id);
        (await check.ContactAddresses.IgnoreQueryFilters().Where(ca => ca.ContactId == both).ToListAsync())
            .Should().ContainSingle().Which.AddressId.Should().Be(shortForm.Id);
    }

    private async Task<Address> NewAddressAsync(IAddressHasher hasher, string line1)
    {
        await using var db = fixture.CreateDbContext();
        return await db.FindOrCreateAsync(hasher, new AddressIdentity(line1, "Springfield", "IL", "62701", "USA"), default);
    }

    /// <summary>A canonicalizer that only knows "Street" is "St".</summary>
    private sealed class StreetToSt : IAddressCanonicalizer
    {
        public string ProviderName => "StreetToSt";

        public Task<CanonicalAddressComponents> CanonicalizeAsync(AddressComponentsInput input, CancellationToken ct = default) =>
            Task.FromResult(new CanonicalAddressComponents(
                input.Line1?.Replace("Street", "St"), input.City, input.State, input.PostalCode, input.Country));
    }
}
