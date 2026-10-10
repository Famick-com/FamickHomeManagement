using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.DTOs.Server;
using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Platform;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class WizardServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Mock<ITenantProvider> _tenantProvider;
    private readonly Mock<IContactService> _contactService;
    private readonly Mock<IUserManagementService> _userManagementService;
    private readonly Mock<IMealTypeService> _mealTypeService;
    private readonly WizardService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private Guid? _currentUserId;

    public WizardServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        // Who is signed in. Defaults to the household's only user, which is what nearly every
        // test seeds; a test with more than one sets _currentUserId to say which one is asking.
        _tenantProvider
            .Setup(t => t.UserId)
            .Returns(() => _currentUserId ?? _context.Users.FirstOrDefault(u => u.TenantId == _tenantId)?.Id);

        _contactService = new Mock<IContactService>();
        // Behave like the real ContactService: hand back the household contact, creating it on
        // first ask. Several wizard steps resolve the household this way.
        _contactService
            .Setup(c => c.EnsureTenantHouseholdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (string name, CancellationToken ct) =>
            {
                var household = await _context.Contacts
                    .FirstOrDefaultAsync(c => c.TenantId == _tenantId && c.IsTenantHousehold, ct);

                if (household == null)
                {
                    household = new Contact
                    {
                        Id = Guid.NewGuid(),
                        TenantId = _tenantId,
                        ContactType = ContactType.Household,
                        IsTenantHousehold = true,
                        IsActive = true
                    };
                    _context.Contacts.Add(household);
                }

                household.CompanyName = name;
                await _context.SaveChangesAsync(ct);

                return new ContactDto { Id = household.Id, CompanyName = name, IsTenantHousehold = true };
            });

        // Behave like the real ContactService: a contact linked back to the user.
        _contactService
            .Setup(c => c.CreateContactForUserAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Returns(async (User user, CancellationToken ct) =>
            {
                var contact = new Contact
                {
                    Id = Guid.NewGuid(),
                    TenantId = user.TenantId,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    LinkedUserId = user.Id,
                    IsActive = true
                };
                _context.Contacts.Add(contact);
                user.ContactId = contact.Id;
                await _context.SaveChangesAsync(ct);

                return new ContactDto { Id = contact.Id, FirstName = contact.FirstName, LastName = contact.LastName };
            });

        _contactService
            .Setup(c => c.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, CancellationToken ct) =>
            {
                var contact = await _context.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (contact != null)
                {
                    _context.Contacts.Remove(contact);
                    await _context.SaveChangesAsync(ct);
                }
            });

        _userManagementService = new Mock<IUserManagementService>();
        _mealTypeService = new Mock<IMealTypeService>();

        var logger = new Mock<ILogger<WizardService>>();

        var fileStorageService = new Mock<IFileStorageService>();
        var addressHasher = new AddressHasher(new PassThroughAddressCanonicalizer());

        var serverConfigService = new Mock<IServerConfigService>();
        serverConfigService.Setup(s => s.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ServerConfigDto());

        _service = new WizardService(
            _context,
            _tenantProvider.Object,
            _contactService.Object,
            _userManagementService.Object,
            _mealTypeService.Object,
            fileStorageService.Object,
            addressHasher,
            serverConfigService.Object,
            new PlatformInfo(ServerPlatform.SelfHosted),
            logger.Object);
    }

    /// <summary>
    /// A service scoped to a specific household on a specific kind of server, sharing this
    /// test's database so several households can be observed side by side.
    /// </summary>
    private WizardService BuildServiceFor(
        Guid tenantId,
        ServerPlatform platform,
        Mock<IServerConfigService>? serverConfig = null)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.TenantId).Returns(tenantId);
        tenantProvider
            .Setup(t => t.UserId)
            .Returns(() => _context.Users.FirstOrDefault(u => u.TenantId == tenantId)?.Id);

        serverConfig ??= NewServerConfigService();

        return new WizardService(
            _context,
            tenantProvider.Object,
            _contactService.Object,
            _userManagementService.Object,
            _mealTypeService.Object,
            new Mock<IFileStorageService>().Object,
            new AddressHasher(new PassThroughAddressCanonicalizer()),
            serverConfig.Object,
            new PlatformInfo(platform),
            new Mock<ILogger<WizardService>>().Object);
    }

    private static Mock<IServerConfigService> NewServerConfigService()
    {
        var mock = new Mock<IServerConfigService>();
        mock.Setup(s => s.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ServerConfigDto());
        return mock;
    }

    private async Task<Guid> SeedTenantAsync(string name, string? timeZoneId = null)
    {
        var id = Guid.NewGuid();
        var tenant = new Tenant { Id = id, Name = name };
        if (timeZoneId != null) tenant.TimeZoneId = timeZoneId;

        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedCurrentUserAsync()
    {
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, Email = "alex@test.com", TenantId = _tenantId });
        await _context.SaveChangesAsync();
        return userId;
    }

    private async Task<Guid> SeedTenantHouseholdAsync()
    {
        var householdId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = householdId,
            TenantId = _tenantId,
            CompanyName = "Test Household",
            ContactType = ContactType.Household,
            IsTenantHousehold = true,
            IsActive = true
        });
        await _context.SaveChangesAsync();
        return householdId;
    }

    private async Task SeedTenant()
    {
        _context.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Test Household"
        });
        await _context.SaveChangesAsync();
    }

    #region GetWizardState

    [Fact]
    public async Task GetWizardStateAsync_ShouldReturnState()
    {
        await SeedTenant();

        var result = await _service.GetWizardStateAsync();

        result.Should().NotBeNull();
        result.IsComplete.Should().BeFalse();
        result.HouseholdInfo.Should().NotBeNull();
        result.HouseholdInfo.TenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task GetWizardStateAsync_WithHome_ShouldReturnIsComplete()
    {
        await SeedTenant();
        _context.Homes.Add(new Home { Id = Guid.NewGuid(), IsSetupComplete = true, TenantId = _tenantId });
        await _context.SaveChangesAsync();

        var result = await _service.GetWizardStateAsync();

        result.IsComplete.Should().BeTrue();
    }

    [Fact]
    public async Task GetWizardStateAsync_NullTenantId_ShouldThrow()
    {
        _tenantProvider.Setup(t => t.TenantId).Returns((Guid?)null);

        var act = () => _service.GetWizardStateAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Tenant ID is required");
    }

    #endregion

    #region GetHouseholdMembers

    [Fact]
    public async Task GetHouseholdMembersAsync_WithTheContactCloudRegistrationCreated_ListsIt()
    {
        await SeedTenant();
        var userId = await SeedCurrentUserAsync();

        // What a freshly provisioned cloud household looks like before the wizard runs: one
        // contact holding the admin's sign-in, no household group to parent it to.
        _context.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Alex",
            LastName = "Morgan",
            LinkedUserId = userId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var members = await _service.GetHouseholdMembersAsync();

        members.Should().HaveCount(1);
        members[0].FirstName.Should().Be("Alex");
        members[0].IsCurrentUser.Should().BeTrue();
        members[0].HasUserAccount.Should().BeTrue();
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_CallsTheRequestingUserSelf_NotWhicheverUserComesFirst()
    {
        await SeedTenant();
        var householdId = await SeedTenantHouseholdAsync();

        var firstUserId = await SeedCurrentUserAsync();
        var askingUserId = Guid.NewGuid();
        _context.Users.Add(new User { Id = askingUserId, Email = "sam@test.com", TenantId = _tenantId });

        _context.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Alex",
            LinkedUserId = firstUserId,
            ParentContactId = householdId,
            IsActive = true
        });
        _context.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Sam",
            LinkedUserId = askingUserId,
            ParentContactId = householdId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        _currentUserId = askingUserId;

        var members = await _service.GetHouseholdMembersAsync();

        members.Should().HaveCount(2);
        members.Single(m => m.IsCurrentUser).FirstName.Should().Be("Sam");
    }

    [Fact]
    public async Task GetHouseholdMembersAsync_ShouldNotListGroupContacts()
    {
        await SeedTenant();
        await SeedCurrentUserAsync();
        await SeedTenantHouseholdAsync();

        _context.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyName = "Grandparents",
            ContactType = ContactType.Household,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var members = await _service.GetHouseholdMembersAsync();

        members.Should().BeEmpty();
    }

    #endregion

    #region SaveHouseholdInfo

    [Fact]
    public async Task SaveHouseholdInfoAsync_ShouldUpdateTenantAndCreateAddress()
    {
        await SeedTenant();

        await _service.SaveHouseholdInfoAsync(new HouseholdInfoDto
        {
            TenantId = _tenantId,
            Name = "The Smiths",
            Street1 = "123 Main St",
            City = "Anytown",
            State = "CA",
            PostalCode = "90210",
            Country = "US"
        });

        var tenant = await _context.Tenants.Include(t => t.Address).FirstAsync(t => t.Id == _tenantId);
        tenant.Name.Should().Be("The Smiths");
        tenant.Address.Should().NotBeNull();
        tenant.Address!.AddressLine1.Should().Be("123 Main St");
        tenant.Address.City.Should().Be("Anytown");
    }

    [Fact]
    public async Task SaveHouseholdInfoAsync_ExistingAddress_ShouldUpdate()
    {
        var addressId = Guid.NewGuid();
        var address = new Address { Id = addressId, AddressLine1 = "Old St" };
        _context.Addresses.Add(address);
        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "Old", AddressId = addressId, Address = address });
        await _context.SaveChangesAsync();

        await _service.SaveHouseholdInfoAsync(new HouseholdInfoDto
        {
            TenantId = _tenantId,
            Name = "Updated",
            Street1 = "456 New Ave"
        });

        var tenant = await _context.Tenants.Include(t => t.Address).FirstAsync(t => t.Id == _tenantId);
        tenant.Name.Should().Be("Updated");
        tenant.Address!.AddressLine1.Should().Be("456 New Ave");
    }

    [Fact]
    public async Task SaveHouseholdInfoAsync_TenantNotFound_ShouldThrow()
    {
        var act = () => _service.SaveHouseholdInfoAsync(new HouseholdInfoDto { Name = "Test" });

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }


    [Fact]
    public async Task SaveHouseholdInfoAsync_NamesTheHouseholdContactAfterTheHousehold()
    {
        await SeedTenant();

        await _service.SaveHouseholdInfoAsync(new HouseholdInfoDto
        {
            TenantId = _tenantId,
            Name = "Maple Street Household"
        });

        // No stored " Household" suffix — it used to be re-applied on every wizard run, so a
        // household already named "... Household" grew a second one.
        var household = await _context.Contacts.FirstAsync(c => c.IsTenantHousehold);
        household.CompanyName.Should().Be("Maple Street Household");
    }

    #endregion

    [Fact]
    public async Task SaveHouseholdInfoAsync_FilesAMemberWithAnAccountUnderTheHousehold()
    {
        await SeedTenant();
        var userId = await SeedCurrentUserAsync();

        // The cloud writes this contact at provisioning time, with no group to file it under.
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = _tenantId,
            FirstName = "Alex",
            LinkedUserId = userId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        await _service.SaveHouseholdInfoAsync(new HouseholdInfoDto { Name = "Maple Street Household" });

        var household = await _context.Contacts
            .FirstAsync(c => c.TenantId == _tenantId && c.IsTenantHousehold);
        var contact = await _context.Contacts.FirstAsync(c => c.Id == contactId);

        contact.ParentContactId.Should().Be(household.Id);
        contact.HouseholdTenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task SaveHouseholdInfoAsync_LeavesAContactWithNoAccountWhereItIs()
    {
        await SeedTenant();
        await SeedCurrentUserAsync();

        // A group of its own, and a person nobody has filed yet: neither holds a sign-in, so
        // neither is something this step should claim for the household.
        var groupId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = groupId,
            TenantId = _tenantId,
            CompanyName = "Grandparents",
            ContactType = ContactType.Household,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        await _service.SaveHouseholdInfoAsync(new HouseholdInfoDto { Name = "Maple Street Household" });

        var group = await _context.Contacts.FirstAsync(c => c.Id == groupId);
        group.ParentContactId.Should().BeNull();
    }

    #region SaveCurrentUserContact

    [Fact]
    public async Task SaveCurrentUserContactAsync_BeforeTheHouseholdInfoStep_StillLinksTheUser()
    {
        await SeedTenant();
        var userId = await SeedCurrentUserAsync();

        // No household contact yet: the member step can be reached before the household step.
        var result = await _service.SaveCurrentUserContactAsync(new SaveCurrentUserContactRequest
        {
            FirstName = "Alex",
            LastName = "Morgan"
        });

        var household = await _context.Contacts.FirstAsync(c => c.IsTenantHousehold);
        household.CompanyName.Should().Be("Test Household");

        var contact = await _context.Contacts.FirstAsync(c => c.Id == result.ContactId);
        contact.ParentContactId.Should().Be(household.Id);
        contact.HouseholdTenantId.Should().Be(_tenantId);
        contact.LinkedUserId.Should().Be(userId);
    }

    [Fact]
    public async Task SaveCurrentUserContactAsync_WithTheContactCloudRegistrationCreated_ParentsIt()
    {
        await SeedTenant();
        var userId = await SeedCurrentUserAsync();
        var householdId = await SeedTenantHouseholdAsync();

        // Cloud registration creates the admin's contact before the wizard ever runs, and it
        // arrives here with no parent.
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            TenantId = _tenantId,
            FirstName = "Alex",
            LastName = "Morgan",
            LinkedUserId = userId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.SaveCurrentUserContactAsync(new SaveCurrentUserContactRequest
        {
            FirstName = "Alex",
            LastName = "Morgan"
        });

        result.ContactId.Should().Be(contactId);

        var contact = await _context.Contacts.FirstAsync(c => c.Id == contactId);
        contact.ParentContactId.Should().Be(householdId);
    }

    [Fact]
    public async Task SaveCurrentUserContactAsync_LeavesAMemberFiledUnderAnotherGroupAlone()
    {
        await SeedTenant();
        var userId = await SeedCurrentUserAsync();
        await SeedTenantHouseholdAsync();

        var otherGroupId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = otherGroupId,
            TenantId = _tenantId,
            CompanyName = "Grandparents",
            ContactType = ContactType.Household,
            IsActive = true
        });
        _context.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = "Alex",
            LinkedUserId = userId,
            ParentContactId = otherGroupId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.SaveCurrentUserContactAsync(new SaveCurrentUserContactRequest
        {
            FirstName = "Alex"
        });

        var contact = await _context.Contacts.FirstAsync(c => c.Id == result.ContactId);
        contact.ParentContactId.Should().Be(otherGroupId);
    }

    #endregion

    #region SaveHomeStatistics

    [Fact]
    public async Task SaveHomeStatisticsAsync_NoHome_ShouldCreate()
    {
        var stats = new HomeStatisticsDto
        {
            SquareFootage = 2000,
            YearBuilt = 1990,
            Bedrooms = 3,
            Bathrooms = 2.5m
        };

        await _service.SaveHomeStatisticsAsync(stats);

        var home = await _context.Homes.FirstOrDefaultAsync();
        home.Should().NotBeNull();
        home!.SquareFootage.Should().Be(2000);
        home.YearBuilt.Should().Be(1990);
        home.Bedrooms.Should().Be(3);
    }

    [Fact]
    public async Task SaveHomeStatisticsAsync_ExistingHome_ShouldUpdate()
    {
        _context.Homes.Add(new Home { Id = Guid.NewGuid(), SquareFootage = 1500, TenantId = _tenantId });
        await _context.SaveChangesAsync();

        await _service.SaveHomeStatisticsAsync(new HomeStatisticsDto { SquareFootage = 2500 });

        var home = await _context.Homes.FirstAsync();
        home.SquareFootage.Should().Be(2500);
    }

    #endregion

    #region SaveMaintenanceItems

    [Fact]
    public async Task SaveMaintenanceItemsAsync_NoHome_ShouldCreate()
    {
        await _service.SaveMaintenanceItemsAsync(new MaintenanceItemsDto
        {
            AcFilterSizes = "20x25x1",
            FridgeWaterFilterType = "Samsung DA29"
        });

        var home = await _context.Homes.FirstOrDefaultAsync();
        home.Should().NotBeNull();
        home!.AcFilterSizes.Should().Be("20x25x1");
        home.FridgeWaterFilterType.Should().Be("Samsung DA29");
    }

    [Fact]
    public async Task SaveMaintenanceItemsAsync_EveryFieldTheStepCollects_ComesBackInTheState()
    {
        await SeedTenant();
        var saved = new MaintenanceItemsDto
        {
            AcFilterSizes = "20x25x1",
            HeatingType = "Heat Pump",
            AcType = "Central",
            FridgeWaterFilterType = "Samsung DA29",
            UnderSinkFilterType = "Culligan US-EZ-4",
            WholeHouseFilterType = "Pentair 20in",
            WaterHeaterType = "Tankless",
            WaterHeaterSize = "50 gal",
            SmokeCoDetectorBatteryType = "9V"
        };

        await _service.SaveMaintenanceItemsAsync(saved);
        var state = await _service.GetWizardStateAsync();

        state.MaintenanceItems.Should().BeEquivalentTo(saved);
    }

    #endregion

    #region CompleteWizard

    [Fact]
    public async Task CompleteWizardAsync_NoHome_ShouldCreateWithSetupComplete()
    {
        await _service.CompleteWizardAsync();

        var home = await _context.Homes.FirstOrDefaultAsync();
        home.Should().NotBeNull();
        home!.IsSetupComplete.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteWizardAsync_ExistingHome_ShouldSetComplete()
    {
        _context.Homes.Add(new Home { Id = Guid.NewGuid(), IsSetupComplete = false, TenantId = _tenantId });
        await _context.SaveChangesAsync();

        await _service.CompleteWizardAsync();

        var home = await _context.Homes.FirstAsync();
        home.IsSetupComplete.Should().BeTrue();
    }

    [Fact]
    public async Task CompleteWizardAsync_ShouldSeedDefaultMealTypes()
    {
        await _service.CompleteWizardAsync();

        _mealTypeService.Verify(
            s => s.SeedDefaultsForTenantAsync(_tenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region RemoveHouseholdMember

    [Fact]
    public async Task RemoveHouseholdMemberAsync_ShouldRemoveTheMemberForGood()
    {
        await SeedTenant();
        var householdId = await SeedTenantHouseholdAsync();

        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            FirstName = "John",
            HouseholdTenantId = _tenantId,
            ParentContactId = householdId,
            TenantId = _tenantId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        await _service.RemoveHouseholdMemberAsync(contactId);

        // Clearing HouseholdTenantId was not enough — the member list also matches on the
        // household being the parent group, so an unlinked member came straight back.
        (await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId)).Should().BeNull();
        (await _service.GetHouseholdMembersAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveHouseholdMemberAsync_WithAUserAccount_ShouldRefuse()
    {
        await SeedTenant();
        var householdId = await SeedTenantHouseholdAsync();
        var userId = await SeedCurrentUserAsync();

        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            FirstName = "Alex",
            LinkedUserId = userId,
            HouseholdTenantId = _tenantId,
            ParentContactId = householdId,
            TenantId = _tenantId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var act = () => _service.RemoveHouseholdMemberAsync(contactId);

        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        (await _context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId)).Should().NotBeNull();
    }

    [Fact]
    public async Task RemoveHouseholdMemberAsync_NotFound_ShouldThrow()
    {
        var act = () => _service.RemoveHouseholdMemberAsync(Guid.NewGuid());
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region CheckDuplicateContact

    [Fact]
    public async Task CheckDuplicateContactAsync_MatchFound_ShouldReturnDuplicates()
    {
        _context.Contacts.Add(new Contact
        {
            Id = Guid.NewGuid(),
            FirstName = "Jane",
            LastName = "Doe",
            TenantId = _tenantId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.CheckDuplicateContactAsync(new CheckDuplicateContactRequest
        {
            FirstName = "jane",
            LastName = "doe"
        });

        result.HasDuplicates.Should().BeTrue();
        result.Matches.Should().HaveCount(1);
        result.Matches[0].FirstName.Should().Be("Jane");
    }

    [Fact]
    public async Task CheckDuplicateContactAsync_NoMatch_ShouldReturnEmpty()
    {
        var result = await _service.CheckDuplicateContactAsync(new CheckDuplicateContactRequest
        {
            FirstName = "Nobody"
        });

        result.HasDuplicates.Should().BeFalse();
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckDuplicateContactAsync_NullTenantId_ShouldThrow()
    {
        _tenantProvider.Setup(t => t.TenantId).Returns((Guid?)null);

        var act = () => _service.CheckDuplicateContactAsync(new CheckDuplicateContactRequest { FirstName = "Test" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region AddHouseholdMember

    [Fact]
    public async Task AddHouseholdMemberAsync_NewContact_ShouldCreate()
    {
        await SeedTenant();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, Email = "test@test.com", TenantId = _tenantId });
        await _context.SaveChangesAsync();

        var result = await _service.AddHouseholdMemberAsync(new AddHouseholdMemberRequest
        {
            FirstName = "Jane",
            LastName = "Doe"
        });

        result.Should().NotBeNull();
        result.FirstName.Should().Be("Jane");
        result.IsCurrentUser.Should().BeFalse();

        var contact = await _context.Contacts.FirstAsync(c => c.FirstName == "Jane");
        contact.HouseholdTenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task AddHouseholdMemberAsync_ExistingContact_ShouldLink()
    {
        await SeedTenant();
        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, Email = "test@test.com", TenantId = _tenantId });
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            FirstName = "Existing",
            TenantId = _tenantId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        var result = await _service.AddHouseholdMemberAsync(new AddHouseholdMemberRequest
        {
            FirstName = "Existing",
            ExistingContactId = contactId
        });

        result.ContactId.Should().Be(contactId);

        var contact = await _context.Contacts.FindAsync(contactId);
        contact!.HouseholdTenantId.Should().Be(_tenantId);
    }

    [Fact]
    public async Task AddHouseholdMemberAsync_BeforeTheHouseholdInfoStep_CreatesAndLinksTheHousehold()
    {
        await SeedTenant();
        await SeedCurrentUserAsync();

        var result = await _service.AddHouseholdMemberAsync(new AddHouseholdMemberRequest
        {
            FirstName = "Jane",
            LastName = "Morgan"
        });

        var household = await _context.Contacts.FirstAsync(c => c.IsTenantHousehold);
        var contact = await _context.Contacts.FirstAsync(c => c.Id == result.ContactId);
        contact.ParentContactId.Should().Be(household.Id);
    }

    [Fact]
    public async Task AddHouseholdMemberAsync_NullTenantId_ShouldThrow()
    {
        _tenantProvider.Setup(t => t.TenantId).Returns((Guid?)null);

        var act = () => _service.AddHouseholdMemberAsync(new AddHouseholdMemberRequest { FirstName = "Test" });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    #region Time zone ownership

    [Fact]
    public async Task SaveServerSetupAsync_StoresTheTimeZoneOnTheHousehold()
    {
        // Tenant.TimeZoneId is what the application reads. Writing only the server-level value,
        // as this once did, left the wizard step changing nothing the user would ever see.
        await SeedTenant();

        await _service.SaveServerSetupAsync(new ServerSetupDto
        {
            PublicHostName = "https://home.example",
            TimeZone = "Europe/London"
        });

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == _tenantId);
        tenant.TimeZoneId.Should().Be("Europe/London");
    }

    [Fact]
    public async Task SaveServerSetupAsync_OnAServerHoldingManyHouseholds_LeavesOneHouseholdsZoneToItself()
    {
        // The regression this exists for: the wizard used to write a single shared file, so the
        // last household through set the time zone for everyone.
        var london = await SeedTenantAsync("London Household");
        var tokyo = await SeedTenantAsync("Tokyo Household");

        await BuildServiceFor(london, ServerPlatform.Cloud)
            .SaveServerSetupAsync(new ServerSetupDto { TimeZone = "Europe/London" });

        await BuildServiceFor(tokyo, ServerPlatform.Cloud)
            .SaveServerSetupAsync(new ServerSetupDto { TimeZone = "Asia/Tokyo" });

        (await _context.Tenants.FirstAsync(t => t.Id == london)).TimeZoneId.Should().Be("Europe/London");
        (await _context.Tenants.FirstAsync(t => t.Id == tokyo)).TimeZoneId.Should().Be("Asia/Tokyo");
    }

    [Fact]
    public async Task SaveServerSetupAsync_OnAServerHoldingManyHouseholds_DoesNotWriteTheSharedServerConfig()
    {
        // There is no single answer to store there, so it must not be touched at all.
        var tenantId = await SeedTenantAsync("Some Household");
        var serverConfig = NewServerConfigService();

        await BuildServiceFor(tenantId, ServerPlatform.Cloud, serverConfig)
            .SaveServerSetupAsync(new ServerSetupDto { TimeZone = "Europe/London" });

        serverConfig.Verify(
            s => s.UpdateAsync(It.IsAny<ServerConfigDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SaveServerSetupAsync_OnASingleHouseholdServer_KeepsTheServerLevelValueInStep()
    {
        // One household means the admin settings page and the household must not disagree.
        var tenantId = await SeedTenantAsync("Only Household");
        var serverConfig = NewServerConfigService();

        await BuildServiceFor(tenantId, ServerPlatform.SelfHosted, serverConfig)
            .SaveServerSetupAsync(new ServerSetupDto
            {
                PublicHostName = "https://home.example",
                TimeZone = "Europe/London"
            });

        serverConfig.Verify(
            s => s.UpdateAsync(
                It.Is<ServerConfigDto>(c => c.Server.TimeZone == "Europe/London"
                                            && c.Server.PublicHostName == "https://home.example"),
                It.IsAny<CancellationToken>()),
            Times.Once);

        (await _context.Tenants.FirstAsync(t => t.Id == tenantId)).TimeZoneId.Should().Be("Europe/London");
    }

    [Fact]
    public async Task GetWizardStateAsync_ShowsTheHouseholdsOwnTimeZone()
    {
        // The step has to show what the save writes, or it reports a value the app never uses.
        var tenantId = await SeedTenantAsync("Zoned Household", "Asia/Tokyo");

        var state = await BuildServiceFor(tenantId, ServerPlatform.Cloud).GetWizardStateAsync();

        state.ServerSetup.TimeZone.Should().Be("Asia/Tokyo");
    }

    #endregion
}
