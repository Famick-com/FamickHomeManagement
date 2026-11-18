using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.MealPlanner;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Every path that changes a contact, or a detail stored under it, reaches the change sink once
/// the save has gone through. Commit and rollback are covered against Postgres, since the
/// in-memory provider has no transactions.
/// </summary>
public class ContactChangeInterceptorTests : IDisposable
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    private readonly RecordingSink _sink = new();
    private readonly ServiceProvider _services;
    private readonly HomeManagementDbContext _context;
    private readonly ContactService _service;
    private readonly DietaryProfileService _dietary;

    private Contact _household = null!;
    private Contact _contact = null!;

    public ContactChangeInterceptorTests()
    {
        _services = new ServiceCollection()
            .AddSingleton<IContactChangeSink>(_sink)
            .AddLogging()
            .BuildServiceProvider();

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        tenantProvider.Setup(t => t.UserId).Returns(_userId);

        _context = new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(new ContactChangeInterceptor(_services))
                .Options,
            tenantProvider.Object);

        _service = new ContactService(
            _context,
            tenantProvider.Object,
            Mock.Of<IFileStorageService>(),
            Mock.Of<IFileUrlService>(),
            new AddressHasher(new PassThroughAddressCanonicalizer()),
            Mock.Of<ILogger<ContactService>>());

        _dietary = new DietaryProfileService(_context, tenantProvider.Object, Mock.Of<ILogger<DietaryProfileService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
        _services.Dispose();
    }

    [Fact]
    public async Task CreatingAContactReportsIt()
    {
        await SeedAsync();

        var created = await _service.CreateAsync(new CreateContactRequest { FirstName = "New" });

        _sink.Reported.Should().Contain((_tenantId, created.Id));
    }

    [Fact]
    public async Task ChangingADetailStoredUnderAContactReportsTheContact()
    {
        await SeedAsync();

        await _service.AddPhoneAsync(_contact.Id, new AddPhoneRequest { PhoneNumber = "555-0100" });

        _sink.Reported.Should().Equal((_tenantId, _contact.Id));
    }

    [Fact]
    public async Task ADietaryChangeIsReportedUnderTheContactsHousehold()
    {
        // Allergens carry no tenant column of their own; the contact's is used.
        await SeedAsync();

        await _dietary.UpdateAsync(_contact.Id, new UpdateDietaryProfileRequest
        {
            Allergens = [new UpdateContactAllergenRequest { AllergenType = AllergenType.Peanuts }]
        });

        _sink.Reported.Should().Contain((_tenantId, _contact.Id));
    }

    [Fact]
    public async Task AnAllergenSavedWithoutItsContactLoadedIsStillReportedUnderItsHousehold()
    {
        await SeedAsync();
        _context.ChangeTracker.Clear();

        _context.ContactAllergens.Add(new ContactAllergen { ContactId = _contact.Id, AllergenType = AllergenType.Milk });
        await _context.SaveChangesAsync();

        _sink.Reported.Should().Equal((_tenantId, _contact.Id));
    }

    [Fact]
    public async Task LeavingTheHouseholdGroupIsReported()
    {
        await SeedAsync();
        var group = await _service.CreateGroupAsync(new CreateContactGroupRequest
        {
            GroupName = "Friends", ContactType = ContactType.Household,
        });
        _sink.Reported.Clear();

        await _service.MoveContactToGroupAsync(_contact.Id, group.Id);

        _sink.Reported.Should().Equal((_tenantId, _contact.Id));
    }

    [Fact]
    public async Task DeletingAContactIsReported()
    {
        await SeedAsync();

        await _service.DeleteAsync(_contact.Id);

        _sink.Reported.Should().Contain((_tenantId, _contact.Id));
    }

    [Fact]
    public async Task ChangesThatAreNotAboutTheContactsDetailsAreNotReported()
    {
        // Tags and the audit log belong to the household holding the contact, not its owner.
        await SeedAsync();
        var tag = await _service.CreateTagAsync(new CreateContactTagRequest { Name = "Family" });

        await _service.AddTagToContactAsync(_contact.Id, tag.Id);

        _sink.Reported.Should().BeEmpty();
    }

    [Fact]
    public async Task ASinkThatFailsDoesNotFailTheSave()
    {
        await SeedAsync();
        _sink.Failure = new InvalidOperationException("queue unavailable");

        var created = await _service.CreateAsync(new CreateContactRequest { FirstName = "Kept" });

        (await _context.Contacts.AnyAsync(c => c.Id == created.Id)).Should().BeTrue();
    }

    private async Task SeedAsync()
    {
        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "Household" });
        _context.Users.Add(new User
        {
            Id = _userId, TenantId = _tenantId, Email = "owner@example.com", FirstName = "Owner",
            LastName = "Member", PasswordHash = "hashed",
        });
        _household = new Contact
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, CompanyName = "Household", ContactType = ContactType.Household,
            IsTenantHousehold = true, CreatedByUserId = _userId, IsActive = true,
        };
        _contact = new Contact
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, FirstName = "Ann", ParentContactId = _household.Id,
            CreatedByUserId = _userId, IsActive = true,
        };
        _context.Contacts.AddRange(_household, _contact);
        await _context.SaveChangesAsync();

        _sink.Reported.Clear();
    }

    private sealed class RecordingSink : IContactChangeSink
    {
        public List<(Guid TenantId, Guid ContactId)> Reported { get; } = new();
        public Exception? Failure { get; set; }

        public Task ContactsChangedAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default)
        {
            if (Failure != null) throw Failure;
            Reported.AddRange(contactIds.Select(id => (tenantId, id)));
            return Task.CompletedTask;
        }
    }
}
