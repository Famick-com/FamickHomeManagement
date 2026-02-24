using Famick.HomeManagement.Core.DTOs.Wizard;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

public class WizardControllerTests
{
    private readonly Mock<IWizardService> _mockService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly WizardController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public WizardControllerTests()
    {
        _mockService = new Mock<IWizardService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var logger = new Mock<ILogger<WizardController>>();

        _controller = new WizardController(
            _mockService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task GetWizardState_ShouldReturnOk()
    {
        _mockService.Setup(s => s.GetWizardStateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WizardStateDto());

        var result = await _controller.GetWizardState();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SaveHouseholdInfo_ShouldReturn204()
    {
        _mockService.Setup(s => s.SaveHouseholdInfoAsync(It.IsAny<HouseholdInfoDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SaveHouseholdInfo(new HouseholdInfoDto { Name = "Test" });

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SaveHomeStatistics_ShouldReturn204()
    {
        _mockService.Setup(s => s.SaveHomeStatisticsAsync(It.IsAny<HomeStatisticsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SaveHomeStatistics(new HomeStatisticsDto());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task SaveMaintenanceItems_ShouldReturn204()
    {
        _mockService.Setup(s => s.SaveMaintenanceItemsAsync(It.IsAny<MaintenanceItemsDto>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.SaveMaintenanceItems(new MaintenanceItemsDto());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetHouseholdMembers_ShouldReturnOk()
    {
        _mockService.Setup(s => s.GetHouseholdMembersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HouseholdMemberDto>());

        var result = await _controller.GetHouseholdMembers();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task SaveCurrentUserContact_ShouldReturnOk()
    {
        _mockService.Setup(s => s.SaveCurrentUserContactAsync(It.IsAny<SaveCurrentUserContactRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto());

        var result = await _controller.SaveCurrentUserContact(new SaveCurrentUserContactRequest { FirstName = "Test" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task AddHouseholdMember_ShouldReturn201()
    {
        _mockService.Setup(s => s.AddHouseholdMemberAsync(It.IsAny<AddHouseholdMemberRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto());

        var result = await _controller.AddHouseholdMember(new AddHouseholdMemberRequest { FirstName = "Jane" });

        result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task UpdateHouseholdMember_ShouldReturnOk()
    {
        var contactId = Guid.NewGuid();
        _mockService.Setup(s => s.UpdateHouseholdMemberAsync(contactId, It.IsAny<UpdateHouseholdMemberRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HouseholdMemberDto());

        var result = await _controller.UpdateHouseholdMember(contactId, new UpdateHouseholdMemberRequest());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task RemoveHouseholdMember_ShouldReturn204()
    {
        var contactId = Guid.NewGuid();
        _mockService.Setup(s => s.RemoveHouseholdMemberAsync(contactId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.RemoveHouseholdMember(contactId);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task CheckDuplicateContact_ShouldReturnOk()
    {
        _mockService.Setup(s => s.CheckDuplicateContactAsync(It.IsAny<CheckDuplicateContactRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DuplicateContactResultDto());

        var result = await _controller.CheckDuplicateContact(new CheckDuplicateContactRequest { FirstName = "Test" });

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CompleteWizard_ShouldReturn204()
    {
        _mockService.Setup(s => s.CompleteWizardAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.CompleteWizard();

        result.Should().BeOfType<NoContentResult>();
    }
}
