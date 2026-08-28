using Famick.HomeManagement.Core.DTOs.Server;
using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Platform;
using Famick.HomeManagement.Domain.Entities;
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

    public WizardServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _contactService = new Mock<IContactService>();
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
    public async Task RemoveHouseholdMemberAsync_ShouldUnlinkFromHousehold()
    {
        var contactId = Guid.NewGuid();
        _context.Contacts.Add(new Contact
        {
            Id = contactId,
            FirstName = "John",
            HouseholdTenantId = _tenantId,
            TenantId = _tenantId,
            IsActive = true
        });
        await _context.SaveChangesAsync();

        await _service.RemoveHouseholdMemberAsync(contactId);

        var contact = await _context.Contacts.FindAsync(contactId);
        contact!.HouseholdTenantId.Should().BeNull();
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
