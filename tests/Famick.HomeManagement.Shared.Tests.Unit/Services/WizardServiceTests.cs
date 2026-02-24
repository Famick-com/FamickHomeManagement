using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
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
    private readonly IMapper _mapper;
    private readonly WizardService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public WizardServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<VehicleMappingProfile>();
        });
        _mapper = config.CreateMapper();

        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _contactService = new Mock<IContactService>();
        _userManagementService = new Mock<IUserManagementService>();

        var logger = new Mock<ILogger<WizardService>>();

        _service = new WizardService(
            _context,
            _tenantProvider.Object,
            _contactService.Object,
            _userManagementService.Object,
            _mapper,
            logger.Object);
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
}
