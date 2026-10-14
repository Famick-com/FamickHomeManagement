using Famick.HomeManagement.Core.DTOs.Common;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Tenant;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Issue #119: <see cref="Address"/> rows carry no tenant id and every household that enters
/// the same address shares one row. No household may change that row, or plant anything on
/// it, that another household then sees. Each household gets its own context over one
/// database, so the tenant filter hides the other's references exactly as in production.
/// </summary>
public class SharedAddressIsolationTests : IDisposable
{
    private static readonly Guid Victim = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid Attacker = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly AddressHasher Hasher = new(new PassThroughAddressCanonicalizer());

    private readonly string _databaseName = $"shared-address-{Guid.NewGuid()}";
    private readonly List<HomeManagementDbContext> _contexts = new();

    public SharedAddressIsolationTests()
    {
        using var db = ContextFor(null);
        db.Tenants.Add(new Tenant { Id = Victim, Name = "Victim" });
        db.Tenants.Add(new Tenant { Id = Attacker, Name = "Attacker" });
        db.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var context in _contexts) context.Dispose();
    }

    private HomeManagementDbContext ContextFor(Guid? tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options;
        var context = new HomeManagementDbContext(options, tenantProvider.Object);
        _contexts.Add(context);
        return context;
    }

    private ContactService ContactServiceFor(Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        return new ContactService(
            ContextFor(tenantId),
            tenantProvider.Object,
            new Mock<IFileStorageService>().Object,
            new Mock<IFileUrlService>().Object,
            Hasher,
            NullLogger<ContactService>.Instance);
    }

    private TenantService TenantServiceFor(Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        return new TenantService(ContextFor(tenantId), tenantProvider.Object, Hasher, NullLogger<TenantService>.Instance);
    }

    private AddressService AddressServiceFor(Guid tenantId)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        var provider = new Mock<IAddressAutocompleteProvider>();
        provider.Setup(p => p.ProviderName).Returns("Geoapify");
        return new AddressService(
            ContextFor(tenantId),
            tenantProvider.Object,
            provider.Object,
            new Mock<IAddressSuggestionCache>().Object,
            Hasher,
            NullLogger<AddressService>.Instance);
    }

    private async Task<Guid> SeedContactAsync(Guid tenantId)
    {
        var db = ContextFor(null);
        var contact = new Contact { Id = Guid.NewGuid(), TenantId = tenantId, FirstName = "Contact", IsActive = true };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    /// <summary>Links a contact to an existing row directly, the way rows came to be shared before the fix.</summary>
    private async Task<Guid> LinkAsync(Guid tenantId, Guid contactId, Guid addressId)
    {
        var db = ContextFor(null);
        var link = new ContactAddress
        {
            Id = Guid.NewGuid(), TenantId = tenantId, ContactId = contactId, AddressId = addressId,
            Tag = AddressTag.Home, IsPrimary = true
        };
        db.ContactAddresses.Add(link);
        await db.SaveChangesAsync();
        return link.Id;
    }

    private async Task<Address> SeedAddressAsync(string line1, string? providerPlaceId = null, string? providerSource = null)
    {
        var db = ContextFor(null);
        var address = new Address
        {
            Id = Guid.NewGuid(),
            AddressLine1 = line1,
            City = "Springfield",
            StateProvince = "IL",
            PostalCode = "62701",
            Country = "USA",
            ProviderPlaceId = providerPlaceId,
            ProviderSource = providerSource,
            NormalizedHash = await Hasher.ComputeAsync(
                new AddressComponentsInput(line1, "Springfield", "IL", "62701", "USA"),
                AddressProvenance.Unverified)
        };
        db.Addresses.Add(address);
        await db.SaveChangesAsync();
        return address;
    }

    private async Task<Address> ReadAddressAsync(Guid id) =>
        await ContextFor(null).Addresses.AsNoTracking().SingleAsync(a => a.Id == id);

    private async Task<ContactAddress> ReadLinkAsync(Guid linkId) =>
        await ContextFor(null).ContactAddresses.IgnoreQueryFilters().AsNoTracking().SingleAsync(ca => ca.Id == linkId);

    private async Task<int> AddressRowCountAsync() =>
        await ContextFor(null).Addresses.CountAsync();

    private static AddContactAddressRequest Request(string line1, string? placeId = null, string? source = null) => new()
    {
        AddressLine1 = line1,
        City = "Springfield",
        StateProvince = "IL",
        PostalCode = "62701",
        Country = "USA",
        ProviderPlaceId = placeId,
        ProviderSource = source,
        Tag = AddressTag.Home,
        IsPrimary = true
    };

    [Fact]
    public async Task Editing_a_shared_address_leaves_the_other_household_untouched()
    {
        var shared = await SeedAddressAsync("1 Victim Street");
        var victimContact = await SeedContactAsync(Victim);
        await LinkAsync(Victim, victimContact, shared.Id);
        var attackerContact = await SeedContactAsync(Attacker);
        var attackerLink = await LinkAsync(Attacker, attackerContact, shared.Id);

        await ContactServiceFor(Attacker).UpdateAddressAsync(attackerContact, attackerLink, Request("666 Attacker Road"));

        var row = await ReadAddressAsync(shared.Id);
        row.AddressLine1.Should().Be("1 Victim Street");
        var victimView = await ContactServiceFor(Victim).GetAddressesAsync(victimContact);
        victimView.Single().Address!.AddressLine1.Should().Be("1 Victim Street");
    }

    [Fact]
    public async Task Two_households_entering_the_same_address_share_one_row()
    {
        var victimContact = await SeedContactAsync(Victim);
        var attackerContact = await SeedContactAsync(Attacker);

        var first = await ContactServiceFor(Victim).AddAddressAsync(victimContact, Request("12 Shared Lane"));
        var second = await ContactServiceFor(Attacker).AddAddressAsync(attackerContact, Request("  12 shared lane "));

        second.AddressId.Should().Be(first.AddressId);
        (await AddressRowCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Client_supplied_provider_fields_never_reach_the_shared_row()
    {
        var attackerContact = await SeedContactAsync(Attacker);
        var victimContact = await SeedContactAsync(Victim);
        var poisoned = Request("5 First Writer Way", placeId: "planted", source: "Geoapify");
        poisoned.Latitude = 1.0;
        poisoned.Longitude = 2.0;
        poisoned.FormattedAddress = "Somewhere else entirely";
        poisoned.CountryCode = "FR";

        var attackerLink = await ContactServiceFor(Attacker).AddAddressAsync(attackerContact, poisoned);
        var victimLink = await ContactServiceFor(Victim).AddAddressAsync(victimContact, Request("5 First Writer Way"));

        victimLink.AddressId.Should().Be(attackerLink.AddressId);
        var row = await ReadAddressAsync(victimLink.AddressId);
        row.Latitude.Should().BeNull();
        row.Longitude.Should().BeNull();
        row.ProviderPlaceId.Should().BeNull();
        row.ProviderSource.Should().BeNull();
        row.FormattedAddress.Should().BeNull();
        row.CountryCode.Should().BeNull();
    }

    [Fact]
    public async Task A_place_id_does_not_pull_in_a_different_address()
    {
        var verified = await SeedAddressAsync("1 Victim Street", providerPlaceId: "real-place", providerSource: "Geoapify");
        var attackerContact = await SeedContactAsync(Attacker);

        var link = await ContactServiceFor(Attacker).AddAddressAsync(
            attackerContact, Request("666 Attacker Road", placeId: "real-place", source: "Geoapify"));

        link.AddressId.Should().NotBe(verified.Id);
        (await ReadAddressAsync(link.AddressId)).AddressLine1.Should().Be("666 Attacker Road");
    }

    [Fact]
    public async Task A_picked_provider_result_is_saved_with_the_providers_coordinates()
    {
        await AddressServiceFor(Victim).RegisterNormalizedAsync([new NormalizedAddressResult
        {
            AddressLine1 = "9 Verified Court",
            City = "Springfield",
            StateProvince = "IL",
            PostalCode = "62701",
            Country = "USA",
            CountryCode = "US",
            Latitude = 39.8,
            Longitude = -89.6,
            ProviderPlaceId = "geo-9",
            ProviderSource = "Geoapify",
            FormattedAddress = "9 Verified Court, Springfield, IL 62701"
        }]);
        var victimContact = await SeedContactAsync(Victim);

        // The web form posts the picked result back as fields.
        var link = await ContactServiceFor(Victim).AddAddressAsync(victimContact, Request("9 Verified Court"));

        var row = await ReadAddressAsync(link.AddressId);
        row.ProviderPlaceId.Should().Be("geo-9");
        row.Latitude.Should().Be(39.8);
        (await AddressRowCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_normalize_fallback_that_echoes_the_input_is_not_recorded()
    {
        await AddressServiceFor(Victim).RegisterNormalizedAsync([new NormalizedAddressResult
        {
            AddressLine1 = "3 Typed Street",
            City = "Springfield",
            Latitude = 1.0,
            MatchType = "fallback"
        }]);

        (await AddressRowCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Units_in_one_building_share_the_row_and_keep_their_own_lines()
    {
        var victimContact = await SeedContactAsync(Victim);
        var attackerContact = await SeedContactAsync(Attacker);
        var upstairs = Request("20 Tower Street");
        upstairs.AddressLine2 = "Apt 1";
        upstairs.AddressLine3 = "c/o Victim";
        var downstairs = Request("20 Tower Street");
        downstairs.AddressLine2 = "Apt 2";

        var victimLink = await ContactServiceFor(Victim).AddAddressAsync(victimContact, upstairs);
        var attackerLink = await ContactServiceFor(Attacker).AddAddressAsync(attackerContact, downstairs);

        attackerLink.AddressId.Should().Be(victimLink.AddressId);
        victimLink.Address!.AddressLine2.Should().Be("Apt 1");
        victimLink.Address.AddressLine3.Should().Be("c/o Victim");
        attackerLink.Address!.AddressLine2.Should().Be("Apt 2");
        attackerLink.Address.AddressLine3.Should().BeNull();
    }

    [Fact]
    public async Task Moving_a_contact_keeps_the_old_row()
    {
        var contact = await SeedContactAsync(Victim);
        var service = ContactServiceFor(Victim);
        var before = await service.AddAddressAsync(contact, Request("1 Old Street"));

        var after = await service.UpdateAddressAsync(contact, before.Id, Request("2 New Street"));

        after.AddressId.Should().NotBe(before.AddressId);
        (await ReadAddressAsync(before.AddressId)).AddressLine1.Should().Be("1 Old Street");
    }

    [Fact]
    public async Task Editing_only_the_unit_lines_keeps_the_row()
    {
        var contact = await SeedContactAsync(Victim);
        var service = ContactServiceFor(Victim);
        var before = await service.AddAddressAsync(contact, Request("1 Same Street"));
        var edit = Request("1 Same Street");
        edit.AddressLine2 = "Suite 5";
        edit.AddressLine4 = "Back door";

        var after = await service.UpdateAddressAsync(contact, before.Id, edit);

        after.AddressId.Should().Be(before.AddressId);
        var link = await ReadLinkAsync(before.Id);
        link.AddressLine2.Should().Be("Suite 5");
        link.AddressLine4.Should().Be("Back door");
        (await AddressRowCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Moving_onto_an_address_the_contact_already_has_is_refused()
    {
        var contact = await SeedContactAsync(Victim);
        var service = ContactServiceFor(Victim);
        await service.AddAddressAsync(contact, Request("1 First Street"));
        var second = await service.AddAddressAsync(contact, Request("2 Second Street"));

        var act = () => service.UpdateAddressAsync(contact, second.Id, Request("1 First Street"));

        await act.Should().ThrowAsync<DuplicateEntityException>();
    }

    [Fact]
    public async Task Linking_another_households_address_by_id_is_allowed()
    {
        var victimContact = await SeedContactAsync(Victim);
        var victimLink = await ContactServiceFor(Victim).AddAddressAsync(victimContact, Request("1 Victim Street"));
        var attackerContact = await SeedContactAsync(Attacker);

        var link = await ContactServiceFor(Attacker).AddAddressAsync(attackerContact,
            new AddContactAddressRequest { AddressId = victimLink.AddressId, Tag = AddressTag.Home });

        link.AddressId.Should().Be(victimLink.AddressId);
        (await ReadAddressAsync(victimLink.AddressId)).AddressLine1.Should().Be("1 Victim Street");
    }

    [Fact]
    public async Task Linking_an_address_id_that_does_not_exist_is_refused()
    {
        var contact = await SeedContactAsync(Victim);

        var act = () => ContactServiceFor(Victim).AddAddressAsync(contact,
            new AddContactAddressRequest { AddressId = Guid.NewGuid(), Tag = AddressTag.Home });

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Changing_the_household_address_leaves_contacts_that_share_the_row_untouched()
    {
        var shared = await SeedAddressAsync("1 Victim Street");
        var victimContact = await SeedContactAsync(Victim);
        await LinkAsync(Victim, victimContact, shared.Id);
        await AttachHouseholdAddressAsync(Attacker, shared.Id);

        await TenantServiceFor(Attacker).UpdateCurrentTenantAsync(new UpdateTenantRequest
        {
            Name = "Attacker",
            Address = new UpdateAddressRequest { AddressLine1 = "666 Attacker Road", City = "Springfield" }
        });

        (await ReadAddressAsync(shared.Id)).AddressLine1.Should().Be("1 Victim Street");
        var victimView = await ContactServiceFor(Victim).GetAddressesAsync(victimContact);
        victimView.Single().Address!.AddressLine1.Should().Be("1 Victim Street");
    }

    [Fact]
    public async Task The_household_shares_the_row_and_keeps_its_own_unit_lines()
    {
        var victimContact = await SeedContactAsync(Victim);
        var contactLink = await ContactServiceFor(Victim).AddAddressAsync(victimContact, Request("30 Block Street"));

        var dto = await TenantServiceFor(Attacker).UpdateCurrentTenantAsync(new UpdateTenantRequest
        {
            Name = "Attacker",
            Address = new UpdateAddressRequest
            {
                AddressLine1 = "30 Block Street", AddressLine2 = "Unit 9", AddressLine3 = "Rear",
                City = "Springfield", StateProvince = "IL", PostalCode = "62701", Country = "USA",
                Latitude = 5.0, ProviderPlaceId = "planted"
            }
        });

        dto.AddressId.Should().Be(contactLink.AddressId);
        dto.Address!.AddressLine2.Should().Be("Unit 9");
        dto.Address.AddressLine3.Should().Be("Rear");
        var row = await ReadAddressAsync(contactLink.AddressId);
        row.Latitude.Should().BeNull();
        row.ProviderPlaceId.Should().BeNull();
    }

    [Fact]
    public async Task Changing_the_household_address_keeps_the_old_row()
    {
        var old = await SeedAddressAsync("1 Old Street");
        await AttachHouseholdAddressAsync(Victim, old.Id);

        await TenantServiceFor(Victim).UpdateCurrentTenantAsync(new UpdateTenantRequest
        {
            Name = "Victim",
            Address = new UpdateAddressRequest { AddressLine1 = "2 New Street", City = "Springfield" }
        });

        (await ReadAddressAsync(old.Id)).AddressLine1.Should().Be("1 Old Street");
    }

    [Fact]
    public async Task Clearing_the_household_address_unlinks_it_without_deleting_the_row()
    {
        var old = await SeedAddressAsync("1 Old Street");
        await AttachHouseholdAddressAsync(Victim, old.Id);

        var dto = await TenantServiceFor(Victim).UpdateCurrentTenantAsync(new UpdateTenantRequest
        {
            Name = "Victim",
            Address = new UpdateAddressRequest()
        });

        dto.AddressId.Should().BeNull();
        dto.Address.Should().BeNull();
        (await ReadAddressAsync(old.Id)).AddressLine1.Should().Be("1 Old Street");
    }

    [Fact]
    public async Task Device_sync_keeps_a_matching_link_and_links_repeats_once()
    {
        var contact = await SeedContactAsync(Victim);
        var service = ContactServiceFor(Victim);
        var existing = await service.AddAddressAsync(contact, Request("1 Kept Street"));
        var entry = (string line1) => new DeviceSyncAddressEntry
        {
            AddressLine1 = line1, City = "Springfield", StateProvince = "IL", PostalCode = "62701", Country = "USA"
        };

        await service.ApplyDeviceSyncUpdateAsync(contact, new DeviceSyncUpdateRequest
        {
            FirstName = "Contact",
            Addresses = [entry("1 KEPT STREET"), entry("2 New Street"), entry("2 new street"), new DeviceSyncAddressEntry()]
        });

        var links = await service.GetAddressesAsync(contact);
        links.Select(l => l.Address!.AddressLine1).Should().BeEquivalentTo("1 Kept Street", "2 New Street");
        links.Should().Contain(l => l.Id == existing.Id);
        (await AddressRowCountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task The_registry_refuses_an_address_with_no_components()
    {
        var act = () => ContextFor(Victim).FindOrCreateAsync(Hasher, new AddressIdentity(" ", null, "", null, null), default);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private async Task AttachHouseholdAddressAsync(Guid tenantId, Guid addressId)
    {
        var db = ContextFor(null);
        var tenant = await db.Tenants.SingleAsync(t => t.Id == tenantId);
        tenant.AddressId = addressId;
        await db.SaveChangesAsync();
    }
}
