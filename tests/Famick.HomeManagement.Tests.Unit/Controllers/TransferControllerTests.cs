using Famick.HomeManagement.Core.DTOs.Transfer;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Controllers;
using Famick.HomeManagement.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Controllers;

public class TransferControllerTests
{
    private readonly Mock<ICloudTransferService> _mockTransferService;
    private readonly Mock<IFeatureManager> _mockFeatureManager;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly Mock<ILogger<TransferController>> _mockLogger;

    private static readonly Guid TestTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public TransferControllerTests()
    {
        _mockTransferService = new Mock<ICloudTransferService>();
        _mockFeatureManager = new Mock<IFeatureManager>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockLogger = new Mock<ILogger<TransferController>>();

        _mockTenantProvider.Setup(x => x.TenantId).Returns(TestTenantId);
    }

    private TransferController CreateController()
    {
        var controller = new TransferController(
            _mockTransferService.Object,
            _mockFeatureManager.Object,
            _mockTenantProvider.Object,
            _mockLogger.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    #region Availability Tests

    [Fact]
    public void GetAvailable_AlwaysReturns200()
    {
        var controller = CreateController();

        var result = controller.GetAvailable();

        result.Should().BeOfType<OkResult>();
    }

    #endregion

    #region Feature Flag Tests

    [Fact]
    public async Task Authenticate_WhenFeatureDisabled_Returns403()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(false);
        var controller = CreateController();

        var result = await controller.Authenticate(new TransferAuthenticateRequest(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetSummary_WhenFeatureDisabled_Returns403()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(false);
        var controller = CreateController();

        var result = await controller.GetSummary(CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetSession_WhenFeatureDisabled_StillReturns200()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(false);
        _mockTransferService.Setup(x => x.GetSessionInfoAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferSessionInfo { HasIncompleteSession = false });
        var controller = CreateController();

        var result = await controller.GetSession(CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Start_WhenFeatureDisabled_Returns403()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(false);
        var controller = CreateController();

        var result = await controller.Start(new TransferStartRequest(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public void GetProgress_WhenFeatureEnabled_ReturnsProgress()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(true);
        var expectedProgress = new TransferProgress
        {
            SessionStatus = TransferSessionStatus.InProgress,
            CurrentCategory = "Products",
            OverallProgressPercent = 50
        };
        _mockTransferService.Setup(x => x.GetCurrentProgress()).Returns(expectedProgress);
        var controller = CreateController();

        var result = controller.GetProgress();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetProgress_WhenNoProgressAvailable_Returns204()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(true);
        _mockTransferService.Setup(x => x.GetCurrentProgress()).Returns((TransferProgress?)null);
        var controller = CreateController();

        var result = controller.GetProgress();

        result.Should().BeOfType<NoContentResult>();
    }

    #endregion

    #region Authenticate Tests

    [Fact]
    public async Task Authenticate_WhenEnabled_CallsService()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(true);
        var request = new TransferAuthenticateRequest
        {
            Email = "test@example.com",
            Password = "password123"
        };
        var expectedResponse = new TransferAuthenticateResponse
        {
            Success = true,
            CloudUserEmail = "test@example.com"
        };
        _mockTransferService
            .Setup(x => x.AuthenticateAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);
        var controller = CreateController();

        var result = await controller.Authenticate(request, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        _mockTransferService.Verify(
            x => x.AuthenticateAsync(request, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Start Tests

    [Fact]
    public async Task Start_WhenServiceThrows_Returns400()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(true);
        _mockTransferService
            .Setup(x => x.StartTransferAsync(It.IsAny<TransferStartRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Must authenticate first"));
        var controller = CreateController();

        var result = await controller.Start(new TransferStartRequest(), CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Start_WhenValid_ReturnsSessionId()
    {
        _mockFeatureManager.Setup(x => x.IsEnabled(FeatureNames.TransferToCloud)).Returns(true);
        var sessionId = Guid.NewGuid();
        _mockTransferService
            .Setup(x => x.StartTransferAsync(It.IsAny<TransferStartRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TransferStartResponse { SessionId = sessionId });
        var controller = CreateController();

        var result = await controller.Start(new TransferStartRequest { IncludeHistory = true }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Cancel Tests

    [Fact]
    public void Cancel_CallsServiceCancelTransfer()
    {
        var controller = CreateController();

        controller.Cancel();

        _mockTransferService.Verify(x => x.CancelTransfer(), Times.Once);
    }

    #endregion
}
