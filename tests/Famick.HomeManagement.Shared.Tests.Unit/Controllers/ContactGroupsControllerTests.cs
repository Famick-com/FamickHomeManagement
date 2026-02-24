using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

public class ContactGroupsControllerTests
{
    private readonly Mock<IContactService> _mockContactService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly ContactsController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public ContactGroupsControllerTests()
    {
        _mockContactService = new Mock<IContactService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var mockFileStorage = new Mock<IFileStorageService>();
        var mockTokenService = new Mock<IFileAccessTokenService>();
        var logger = new Mock<ILogger<ContactsController>>();

        _controller = new ContactsController(
            _mockContactService.Object,
            mockFileStorage.Object,
            mockTokenService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region ListGroups

    [Fact]
    public async Task ListGroups_ShouldReturnOk()
    {
        var groups = new PagedResult<ContactGroupSummaryDto>
        {
            Items = new List<ContactGroupSummaryDto>
            {
                new() { Id = Guid.NewGuid(), GroupName = "Family", ContactType = ContactType.Household, MemberCount = 3 }
            },
            TotalCount = 1,
            Page = 1,
            PageSize = 10
        };

        _mockContactService
            .Setup(s => s.ListGroupsAsync(It.IsAny<ContactFilterRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(groups);

        var result = await _controller.ListGroups(new ContactFilterRequest(), CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var returnedGroups = okResult.Value.Should().BeAssignableTo<PagedResult<ContactGroupSummaryDto>>().Subject;
        returnedGroups.TotalCount.Should().Be(1);
    }

    #endregion

    #region GetGroup

    [Fact]
    public async Task GetGroup_Found_ShouldReturnOk()
    {
        var groupId = Guid.NewGuid();
        var group = new ContactDto
        {
            Id = groupId,
            CompanyName = "Test Group",
            IsGroup = true,
            ContactType = ContactType.Household
        };

        _mockContactService
            .Setup(s => s.GetGroupByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var result = await _controller.GetGroup(groupId, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetGroup_NotFound_ShouldReturn404()
    {
        var groupId = Guid.NewGuid();

        _mockContactService
            .Setup(s => s.GetGroupByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Contact group", groupId));

        var result = await _controller.GetGroup(groupId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region CreateGroup

    [Fact]
    public async Task CreateGroup_Valid_ShouldReturnCreated()
    {
        var request = new CreateContactGroupRequest
        {
            ContactType = ContactType.Business,
            GroupName = "New Business"
        };

        var created = new ContactGroupSummaryDto
        {
            Id = Guid.NewGuid(),
            GroupName = "New Business",
            ContactType = ContactType.Business
        };

        _mockContactService
            .Setup(s => s.CreateGroupAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var result = await _controller.CreateGroup(request, CancellationToken.None);

        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = (CreatedAtActionResult)result;
        createdResult.ActionName.Should().Be(nameof(ContactsController.GetGroup));
    }

    #endregion

    #region UpdateGroup

    [Fact]
    public async Task UpdateGroup_Valid_ShouldReturnNoContent()
    {
        var groupId = Guid.NewGuid();
        var request = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Updated Name",
            IsActive = true
        };

        _mockContactService
            .Setup(s => s.UpdateGroupAsync(groupId, request, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.UpdateGroup(groupId, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateGroup_NotFound_ShouldReturn404()
    {
        var groupId = Guid.NewGuid();
        var request = new UpdateContactGroupRequest
        {
            ContactType = ContactType.Household,
            GroupName = "Name",
            IsActive = true
        };

        _mockContactService
            .Setup(s => s.UpdateGroupAsync(groupId, request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Contact group", groupId));

        var result = await _controller.UpdateGroup(groupId, request, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region DeleteGroup

    [Fact]
    public async Task DeleteGroup_Valid_ShouldReturnNoContent()
    {
        var groupId = Guid.NewGuid();

        _mockContactService
            .Setup(s => s.DeleteGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteGroup(groupId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteGroup_TenantHousehold_ShouldReturnBadRequest()
    {
        var groupId = Guid.NewGuid();

        _mockContactService
            .Setup(s => s.DeleteGroupAsync(groupId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot delete the tenant household group"));

        var result = await _controller.DeleteGroup(groupId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region GetMyHousehold

    [Fact]
    public async Task GetMyHousehold_Found_ShouldReturnOk()
    {
        var household = new ContactDto
        {
            Id = Guid.NewGuid(),
            CompanyName = "My Household",
            IsGroup = true,
            IsTenantHousehold = true
        };

        _mockContactService
            .Setup(s => s.GetTenantHouseholdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(household);

        var result = await _controller.GetMyHousehold(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMyHousehold_NotFound_ShouldReturn404()
    {
        _mockContactService
            .Setup(s => s.GetTenantHouseholdAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Tenant household", Guid.Empty));

        var result = await _controller.GetMyHousehold(CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region MoveToGroup

    [Fact]
    public async Task MoveToGroup_Valid_ShouldReturnNoContent()
    {
        var contactId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _mockContactService
            .Setup(s => s.MoveContactToGroupAsync(contactId, groupId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.MoveToGroup(contactId, groupId, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MoveToGroup_ContactNotFound_ShouldReturn404()
    {
        var contactId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _mockContactService
            .Setup(s => s.MoveContactToGroupAsync(contactId, groupId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException(nameof(Domain.Entities.Contact), contactId));

        var result = await _controller.MoveToGroup(contactId, groupId, CancellationToken.None);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task MoveToGroup_GroupContact_ShouldReturnBadRequest()
    {
        var contactId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _mockContactService
            .Setup(s => s.MoveContactToGroupAsync(contactId, groupId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cannot move a group contact"));

        var result = await _controller.MoveToGroup(contactId, groupId, CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
    }

    #endregion
}
