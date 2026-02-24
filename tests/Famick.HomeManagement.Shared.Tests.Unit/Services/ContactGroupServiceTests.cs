using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

public class ContactGroupServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Mock<ITenantProvider> _tenantProvider;
    private readonly IMapper _mapper;
    private readonly ContactService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public ContactGroupServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantProvider = new Mock<ITenantProvider>();
        _tenantProvider.Setup(t => t.TenantId).Returns(_tenantId);
        _tenantProvider.Setup(t => t.UserId).Returns(_userId);

        _context = new HomeManagementDbContext(options, _tenantProvider.Object);

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<ContactMappingProfile>();
        });
        _mapper = config.CreateMapper();

        var mockFileStorage = new Mock<IFileStorageService>();
        var mockTokenService = new Mock<IFileAccessTokenService>();
        var logger = new Mock<ILogger<ContactService>>();

        _service = new ContactService(
            _context,
            _mapper,
            _tenantProvider.Object,
            mockFileStorage.Object,
            mockTokenService.Object,
            logger.Object);
    }

    private async Task SeedTenantAndUser()
    {
        _context.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Test Household"
        });
        _context.Users.Add(new User
        {
            Id = _userId,
            TenantId = _tenantId,
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "hashed"
        });
        await _context.SaveChangesAsync();
    }

    private async Task<Contact> SeedTenantHousehold()
    {
        var household = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            CompanyName = "Test Household",
            ContactType = ContactType.Household,
            IsTenantHousehold = true,
            CreatedByUserId = _userId,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true
        };
        _context.Contacts.Add(household);
        await _context.SaveChangesAsync();
        return household;
    }

    private async Task<Contact> SeedMemberContact(Guid parentGroupId, string firstName, string lastName)
    {
        var member = new Contact
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            FirstName = firstName,
            LastName = lastName,
            ParentContactId = parentGroupId,
            CreatedByUserId = _userId,
            Visibility = ContactVisibilityLevel.TenantShared,
            IsActive = true
        };
        _context.Contacts.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    #region CreateGroupAsync

    [Fact]
    public async Task CreateGroupAsync_HouseholdGroup_ShouldCreateSuccessfully()
    {
        await SeedTenantAndUser();

        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Smith Family"
        };

        var result = await _service.CreateGroupAsync(request);

        result.Should().NotBeNull();
        result.GroupName.Should().Be("Smith Family");
        result.ContactType.Should().Be(ContactType.Household);
        result.MemberCount.Should().Be(0);
        result.IsTenantHousehold.Should().BeFalse();

        var savedContact = await _context.Contacts.FindAsync(result.Id);
        savedContact.Should().NotBeNull();
        savedContact!.CompanyName.Should().Be("Smith Family");
        savedContact.ContactType.Should().Be(ContactType.Household);
        savedContact.ParentContactId.Should().BeNull();
    }

    [Fact]
    public async Task CreateGroupAsync_BusinessGroup_ShouldSetBusinessFields()
    {
        await SeedTenantAndUser();

        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Acme Corp",
            Website = "https://acme.com",
            BusinessCategory = "Manufacturing"
        };

        var result = await _service.CreateGroupAsync(request);

        result.Should().NotBeNull();
        result.GroupName.Should().Be("Acme Corp");
        result.ContactType.Should().Be(ContactType.Business);
        result.Website.Should().Be("https://acme.com");
        result.BusinessCategory.Should().Be("Manufacturing");
    }

    [Fact]
    public async Task CreateGroupAsync_HouseholdGroup_ShouldNotSetBusinessFields()
    {
        await SeedTenantAndUser();

        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Jones Family",
            Website = "https://jones.com",
            BusinessCategory = "N/A"
        };

        var result = await _service.CreateGroupAsync(request);

        result.Website.Should().BeNull();
        result.BusinessCategory.Should().BeNull();
    }

    #endregion

    #region GetGroupByIdAsync

    [Fact]
    public async Task GetGroupByIdAsync_WithValidGroup_ShouldReturnGroup()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();

        var result = await _service.GetGroupByIdAsync(household.Id);

        result.Should().NotBeNull();
        result.IsGroup.Should().BeTrue();
        result.CompanyName.Should().Be("Test Household");
    }

    [Fact]
    public async Task GetGroupByIdAsync_WithMemberContact_ShouldThrow()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        var member = await SeedMemberContact(household.Id, "John", "Doe");

        var act = async () => await _service.GetGroupByIdAsync(member.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Contact is not a group");
    }

    [Fact]
    public async Task GetGroupByIdAsync_WithNonExistent_ShouldThrow()
    {
        await SeedTenantAndUser();

        var act = async () => await _service.GetGroupByIdAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region ListGroupsAsync

    [Fact]
    public async Task ListGroupsAsync_ShouldReturnOnlyGroups()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        await SeedMemberContact(household.Id, "John", "Doe");
        await SeedMemberContact(household.Id, "Jane", "Doe");

        // Create a second group
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Business Co"
        };
        await _service.CreateGroupAsync(request);

        var filter = new ContactFilterRequest { Page = 1, PageSize = 10 };
        var result = await _service.ListGroupsAsync(filter);

        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(g => g.GroupName == "Test Household" && g.MemberCount == 2);
        result.Items.Should().Contain(g => g.GroupName == "Business Co" && g.MemberCount == 0);
    }

    [Fact]
    public async Task ListGroupsAsync_FilterByContactType_ShouldFilterCorrectly()
    {
        await SeedTenantAndUser();
        await SeedTenantHousehold();

        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Business Co"
        };
        await _service.CreateGroupAsync(request);

        var filter = new ContactFilterRequest
        {
            Page = 1,
            PageSize = 10,
            ContactType = ContactType.Business
        };
        var result = await _service.ListGroupsAsync(filter);

        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].GroupName.Should().Be("Business Co");
    }

    #endregion

    #region UpdateGroupAsync

    [Fact]
    public async Task UpdateGroupAsync_ShouldUpdateFields()
    {
        await SeedTenantAndUser();

        var createRequest = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "Old Name",
            Website = "https://old.com"
        };
        var created = await _service.CreateGroupAsync(createRequest);

        var updateRequest = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "New Name",
            Website = "https://new.com",
            BusinessCategory = "Tech",
            IsActive = true
        };

        await _service.UpdateGroupAsync(created.Id, updateRequest);

        var updated = await _context.Contacts.FindAsync(created.Id);
        updated!.CompanyName.Should().Be("New Name");
        updated.Website.Should().Be("https://new.com");
        updated.BusinessCategory.Should().Be("Tech");
    }

    [Fact]
    public async Task UpdateGroupAsync_MemberContact_ShouldThrow()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        var member = await SeedMemberContact(household.Id, "John", "Doe");

        var updateRequest = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Attempt",
            IsActive = true
        };

        var act = async () => await _service.UpdateGroupAsync(member.Id, updateRequest);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Contact is not a group");
    }

    #endregion

    #region DeleteGroupAsync

    [Fact]
    public async Task DeleteGroupAsync_ShouldMoveMembers_ToTenantHousehold()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();

        // Create a secondary group with members
        var createRequest = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Other Family"
        };
        var otherGroup = await _service.CreateGroupAsync(createRequest);
        var member1 = await SeedMemberContact(otherGroup.Id, "Alice", "Wonder");
        var member2 = await SeedMemberContact(otherGroup.Id, "Bob", "Builder");

        await _service.DeleteGroupAsync(otherGroup.Id);

        // Group should be deleted
        var deletedGroup = await _context.Contacts.FindAsync(otherGroup.Id);
        deletedGroup.Should().BeNull();

        // Members should be moved to tenant household
        var movedMember1 = await _context.Contacts.FindAsync(member1.Id);
        movedMember1!.ParentContactId.Should().Be(household.Id);

        var movedMember2 = await _context.Contacts.FindAsync(member2.Id);
        movedMember2!.ParentContactId.Should().Be(household.Id);
    }

    [Fact]
    public async Task DeleteGroupAsync_TenantHousehold_ShouldThrow()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();

        var act = async () => await _service.DeleteGroupAsync(household.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot delete the tenant household group");
    }

    [Fact]
    public async Task DeleteGroupAsync_MemberContact_ShouldThrow()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        var member = await SeedMemberContact(household.Id, "John", "Doe");

        var act = async () => await _service.DeleteGroupAsync(member.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Contact is not a group");
    }

    [Fact]
    public async Task DeleteGroupAsync_NonExistent_ShouldThrow()
    {
        await SeedTenantAndUser();

        var act = async () => await _service.DeleteGroupAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region MoveContactToGroupAsync

    [Fact]
    public async Task MoveContactToGroupAsync_ShouldMoveToTargetGroup()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        var member = await SeedMemberContact(household.Id, "John", "Doe");

        var newGroup = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "New Family"
        };
        var targetGroup = await _service.CreateGroupAsync(newGroup);

        await _service.MoveContactToGroupAsync(member.Id, targetGroup.Id);

        var movedMember = await _context.Contacts.FindAsync(member.Id);
        movedMember!.ParentContactId.Should().Be(targetGroup.Id);
    }

    [Fact]
    public async Task MoveContactToGroupAsync_GroupContact_ShouldThrow()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();

        var newGroup = new CreateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Target Group"
        };
        var targetGroup = await _service.CreateGroupAsync(newGroup);

        var act = async () => await _service.MoveContactToGroupAsync(household.Id, targetGroup.Id);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Cannot move a group contact*");
    }

    [Fact]
    public async Task MoveContactToGroupAsync_NonExistentTarget_ShouldThrow()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        var member = await SeedMemberContact(household.Id, "John", "Doe");

        var act = async () => await _service.MoveContactToGroupAsync(member.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region EnsureTenantHouseholdAsync

    [Fact]
    public async Task EnsureTenantHouseholdAsync_WhenNoneExists_ShouldCreate()
    {
        await SeedTenantAndUser();

        var result = await _service.EnsureTenantHouseholdAsync("My Household");

        result.Should().NotBeNull();
        result.IsTenantHousehold.Should().BeTrue();
        result.CompanyName.Should().Be("My Household");
        result.IsGroup.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureTenantHouseholdAsync_WhenExists_ShouldUpdateName()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();

        var result = await _service.EnsureTenantHouseholdAsync("Updated Household Name");

        result.Should().NotBeNull();
        result.Id.Should().Be(household.Id);
        result.CompanyName.Should().Be("Updated Household Name");
    }

    [Fact]
    public async Task EnsureTenantHouseholdAsync_WhenExists_SameName_ShouldNotUpdate()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();
        var originalUpdatedAt = household.UpdatedAt;

        var result = await _service.EnsureTenantHouseholdAsync("Test Household");

        result.Should().NotBeNull();
        result.Id.Should().Be(household.Id);
        var dbContact = await _context.Contacts.FindAsync(household.Id);
        dbContact!.UpdatedAt.Should().Be(originalUpdatedAt);
    }

    #endregion

    #region GetTenantHouseholdAsync

    [Fact]
    public async Task GetTenantHouseholdAsync_WhenExists_ShouldReturn()
    {
        await SeedTenantAndUser();
        var household = await SeedTenantHousehold();

        var result = await _service.GetTenantHouseholdAsync();

        result.Should().NotBeNull();
        result.Id.Should().Be(household.Id);
        result.IsTenantHousehold.Should().BeTrue();
    }

    [Fact]
    public async Task GetTenantHouseholdAsync_WhenNoneExists_ShouldThrow()
    {
        await SeedTenantAndUser();

        var act = async () => await _service.GetTenantHouseholdAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    public void Dispose()
    {
        _context.Dispose();
    }
}
