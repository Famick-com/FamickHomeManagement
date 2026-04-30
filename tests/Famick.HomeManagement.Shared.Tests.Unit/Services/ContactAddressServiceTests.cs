using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Tests for <see cref="ContactService.AddAddressAsync"/> focused on the
/// dedup path that the libpostal canonicalizer made common: same contact,
/// two address entries that collapse onto a single Address row.
/// </summary>
public class ContactAddressServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Mock<ITenantProvider> _tenantProvider;
    private readonly ContactService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public ContactAddressServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _tenantProvider.Setup(t => t.UserId).Returns(_userId);

        _context = new HomeManagementDbContext(options, _tenantProvider.Object);

        var mockFileStorage = new Mock<IFileStorageService>();
        var mockFileUrlService = new Mock<IFileUrlService>();
        var logger = new Mock<ILogger<ContactService>>();
        var addressHasher = new AddressHasher(new PassThroughAddressCanonicalizer());

        _service = new ContactService(
            _context,
            _tenantProvider.Object,
            mockFileStorage.Object,
            mockFileUrlService.Object,
            addressHasher,
            logger.Object);
    }

    public void Dispose() => _context.Dispose();

    private async Task<Contact> SeedContact()
    {
        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "Test Household" });
        var contact = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Test",
            LastName = "Contact",
            CreatedByUserId = _userId,
            IsActive = true
        };
        _context.Contacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    /// <summary>
    /// Regression for the bug introduced by libpostal canonicalization: when
    /// the second hand-typed address on a contact canonicalizes to the same
    /// Address row as a previous one, AddAddressAsync used to insert a
    /// duplicate ContactAddress row and crash on the
    /// <c>IX_contact_addresses_ContactId_AddressId</c> unique constraint.
    /// The fix treats the collision as an UPDATE of the existing link.
    /// </summary>
    [Fact]
    public async Task AddAddressAsync_WhenLibpostalCollapsesOntoExistingLink_UpdatesInsteadOfCrashing()
    {
        var contact = await SeedContact();

        // First add — creates Address + ContactAddress as Home/primary.
        var first = await _service.AddAddressAsync(contact.Id, new AddContactAddressRequest
        {
            AddressLine1 = "123 Main St",
            City = "Springfield",
            StateProvince = "IL",
            PostalCode = "62701",
            Country = "USA",
            AddressLine2 = "Apt 4",
            Tag = AddressTag.Home,
            IsPrimary = true
        });

        // Second add — same components → same canonical hash → same
        // AddressId reused. Without the dedup-collision fix this would
        // throw on SaveChanges. We're also changing tag/IsPrimary/Line2
        // to verify the existing link gets updated rather than ignored.
        var second = await _service.AddAddressAsync(contact.Id, new AddContactAddressRequest
        {
            AddressLine1 = "123 Main St",
            City = "Springfield",
            StateProvince = "IL",
            PostalCode = "62701",
            Country = "USA",
            AddressLine2 = "Suite 5",
            Tag = AddressTag.Work,
            IsPrimary = false
        });

        // Same ContactAddress row, just refreshed.
        second.Id.Should().Be(first.Id);
        second.AddressId.Should().Be(first.AddressId);

        // The contact has exactly one address link, not two.
        var linkCount = await _context.ContactAddresses
            .CountAsync(ca => ca.ContactId == contact.Id);
        linkCount.Should().Be(1);

        // The existing link absorbed the second request's fields.
        var link = await _context.ContactAddresses
            .FirstAsync(ca => ca.ContactId == contact.Id);
        link.Tag.Should().Be(AddressTag.Work);
        link.IsPrimary.Should().BeFalse();
        link.AddressLine2.Should().Be("Suite 5");

        // And exactly one Address row was created — the dedup worked.
        var addressCount = await _context.Addresses.CountAsync();
        addressCount.Should().Be(1);
    }

    /// <summary>
    /// Two DIFFERENT contacts adding the same address still produces two
    /// ContactAddress rows pointing at one shared Address — that's the
    /// building-as-row contract working as designed.
    /// </summary>
    [Fact]
    public async Task AddAddressAsync_SameAddressOnTwoContacts_SharesOneAddressRow()
    {
        var contactA = await SeedContact();
        var contactB = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Other",
            LastName = "Contact",
            CreatedByUserId = _userId,
            IsActive = true
        };
        _context.Contacts.Add(contactB);
        await _context.SaveChangesAsync();

        var common = new AddContactAddressRequest
        {
            AddressLine1 = "123 Main St",
            City = "Springfield",
            StateProvince = "IL",
            PostalCode = "62701",
            Country = "USA",
            Tag = AddressTag.Home,
            IsPrimary = true
        };

        var linkA = await _service.AddAddressAsync(contactA.Id, common);
        var linkB = await _service.AddAddressAsync(contactB.Id, common);

        linkA.AddressId.Should().Be(linkB.AddressId); // shared Address row
        linkA.Id.Should().NotBe(linkB.Id);            // distinct ContactAddress rows

        (await _context.Addresses.CountAsync()).Should().Be(1);
        (await _context.ContactAddresses.CountAsync()).Should().Be(2);
    }
}
