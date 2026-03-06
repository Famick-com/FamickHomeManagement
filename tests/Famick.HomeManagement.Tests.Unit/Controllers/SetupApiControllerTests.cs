using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Controllers;

public class SetupApiControllerTests
{
    private readonly Mock<ISetupService> _mockSetupService;
    private readonly Mock<ITenantService> _mockTenantService;
    private readonly Mock<IMultiTenancyOptions> _mockMultiTenancyOptions;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<SetupApiController>> _mockLogger;

    public SetupApiControllerTests()
    {
        _mockSetupService = new Mock<ISetupService>();
        _mockTenantService = new Mock<ITenantService>();
        _mockMultiTenancyOptions = new Mock<IMultiTenancyOptions>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SetupApiController>>();

        // Default: self-hosted mode, no tenant name (falls back to config)
        _mockMultiTenancyOptions.Setup(m => m.IsMultiTenantEnabled).Returns(false);
    }

    private SetupApiController CreateController(string? publicUrl = null, string? serverName = null)
    {
        // Setup configuration
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns(publicUrl);
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns(serverName ?? "Test Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockTenantService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockMultiTenancyOptions.Object);

        // Setup HttpContext
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return controller;
    }

    #region GetMobileAppDeepLink Tests

    [Fact]
    public async Task GetMobileAppDeepLink_WithConfiguredUrl_ReturnsDeepLinkAndSetupPageUrl()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "My Home Server");

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://home.example.com");
        response.ServerName.Should().Be("My Home Server");

        // Verify deep link (direct app link)
        response.DeepLink.Should().Contain("famick://setup?url=");
        response.DeepLink.Should().Contain("https%3a%2f%2fhome.example.com");
        response.DeepLink.Should().Contain("name=My+Home+Server");

        // Verify setup page URL (landing page with app store fallback)
        response.SetupPageUrl.Should().Contain("https://home.example.com/app-setup?url=");
        response.SetupPageUrl.Should().Contain("name=My+Home+Server");
    }

    [Fact]
    public async Task GetMobileAppDeepLink_WithoutConfiguredUrl_FallsBackToRequestHost()
    {
        // Arrange
        var controller = CreateController(publicUrl: null, serverName: "Test Server");

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://localhost:5001");
        response.ServerName.Should().Be("Test Server");
    }

    [Fact]
    public async Task GetMobileAppDeepLink_WithForwardedHeaders_UsesForwardedHost()
    {
        // Arrange
        var controller = CreateController(publicUrl: null, serverName: "Proxied Server");

        // Add forwarded headers
        controller.HttpContext.Request.Headers["X-Forwarded-Host"] = new StringValues("proxy.example.com");
        controller.HttpContext.Request.Headers["X-Forwarded-Proto"] = new StringValues("https");

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://proxy.example.com");
    }

    [Fact]
    public async Task GetMobileAppDeepLink_UrlEncodesSpecialCharacters()
    {
        // Arrange
        var controller = CreateController(
            publicUrl: "https://home.example.com:8443",
            serverName: "John's Home & Kitchen");

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        // The deep link should contain URL-encoded values
        response.DeepLink.Should().Contain("famick://setup?url=");
        response.DeepLink.Should().NotContain(" "); // Spaces should be encoded
        response.DeepLink.Should().NotContain("&name=&"); // Should not have empty or broken params
    }

    #endregion

    #region GetMobileAppConfig Tests

    [Fact]
    public async Task GetMobileAppConfig_WithConfiguredUrl_ReturnsIsEnabledAndIsConfiguredTrue()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act
        var result = await controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.IsEnabled.Should().BeTrue();
        response.IsConfigured.Should().BeTrue();
        response.IsSelfHosted.Should().BeTrue();
        response.ServerUrl.Should().Be("https://home.example.com");
        response.ServerName.Should().Be("Home Server");
        response.DeepLinkScheme.Should().Be("famick");
        response.DeepLinkHost.Should().Be("setup");
    }

    [Fact]
    public async Task GetMobileAppConfig_WithoutConfiguredUrl_StillReturnsIsConfiguredTrue()
    {
        // Arrange - Even without PublicUrl config, falls back to request host
        var controller = CreateController(publicUrl: null, serverName: "Default Server");

        // Act
        var result = await controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        // Falls back to request host, so still configured
        response.IsConfigured.Should().BeTrue();
        response.ServerUrl.Should().Be("https://localhost:5001");
    }

    [Fact]
    public async Task GetMobileAppConfig_ReturnsCorrectDeepLinkScheme()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com");

        // Act
        var result = await controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.DeepLinkScheme.Should().Be("famick");
        response.DeepLinkHost.Should().Be("setup");
    }

    [Fact]
    public async Task GetMobileAppConfig_WhenDisabled_ReturnsIsEnabledFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:Enabled"]).Returns("false");
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns("Home Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockTenantService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockMultiTenancyOptions.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.IsEnabled.Should().BeFalse();
        response.IsConfigured.Should().BeFalse(); // IsConfigured requires IsEnabled
    }

    [Fact]
    public async Task GetMobileAppConfig_InCloudMode_ReturnsIsSelfHostedFalse()
    {
        // Arrange
        _mockMultiTenancyOptions.Setup(m => m.IsMultiTenantEnabled).Returns(true);
        var controller = CreateController(publicUrl: "https://app.famick.com", serverName: "Cloud");

        // Act
        var result = await controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.IsSelfHosted.Should().BeFalse();
    }

    [Fact]
    public async Task GetMobileAppQrCode_WhenDisabled_ReturnsNotFound()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:Enabled"]).Returns("false");
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns("Home Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockTenantService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockMultiTenancyOptions.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.GetMobileAppQrCode();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetMobileAppDeepLink_WhenDisabled_ReturnsNotFound()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:Enabled"]).Returns("false");
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns("Home Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockTenantService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockMultiTenancyOptions.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetMobileAppQrCode Tests

    [Fact]
    public async Task GetMobileAppQrCode_WithConfiguredUrl_ReturnsPngFile()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act
        var result = await controller.GetMobileAppQrCode();

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileDownloadName.Should().Be("famick-setup-qr.png");
        fileResult.FileContents.Should().NotBeEmpty();

        // PNG files start with specific magic bytes
        fileResult.FileContents[0].Should().Be(0x89); // PNG signature
        fileResult.FileContents[1].Should().Be(0x50); // 'P'
        fileResult.FileContents[2].Should().Be(0x4E); // 'N'
        fileResult.FileContents[3].Should().Be(0x47); // 'G'
    }

    [Fact]
    public async Task GetMobileAppQrCode_WithCustomPixelsPerModule_GeneratesDifferentSize()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act
        var smallResult = await controller.GetMobileAppQrCode(pixelsPerModule: 5);
        var largeResult = await controller.GetMobileAppQrCode(pixelsPerModule: 20);

        // Assert
        var smallFile = smallResult.Should().BeOfType<FileContentResult>().Subject;
        var largeFile = largeResult.Should().BeOfType<FileContentResult>().Subject;

        // Larger pixels per module should produce larger file (more image data)
        largeFile.FileContents.Length.Should().BeGreaterThan(smallFile.FileContents.Length);
    }

    [Fact]
    public async Task GetMobileAppQrCode_WithUseLandingPageTrue_GeneratesQrWithLandingPageUrl()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act - Default is useLandingPage: true
        var result = await controller.GetMobileAppQrCode(pixelsPerModule: 10, useLandingPage: true);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetMobileAppQrCode_WithUseLandingPageFalse_GeneratesQrWithDirectDeepLink()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act - Direct deep link QR
        var result = await controller.GetMobileAppQrCode(pixelsPerModule: 10, useLandingPage: false);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    #endregion

    #region URL Normalization Tests

    [Fact]
    public async Task GetMobileAppDeepLink_TrimsTrailingSlash()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com/", serverName: "Test");

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://home.example.com");
        response.ServerUrl.Should().NotEndWith("/");
    }

    [Fact]
    public async Task GetMobileAppDeepLink_UsesConfiguredUrlOverForwardedHeaders()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://configured.example.com", serverName: "Test");

        // Add forwarded headers
        controller.HttpContext.Request.Headers["X-Forwarded-Host"] = new StringValues("proxy.example.com");
        controller.HttpContext.Request.Headers["X-Forwarded-Proto"] = new StringValues("https");

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        // Should use configured URL, not forwarded headers
        response.ServerUrl.Should().Be("https://configured.example.com");
    }

    #endregion

    #region Default Server Name Tests

    [Fact]
    public async Task GetMobileAppDeepLink_WithoutServerName_UsesDefaultName()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns((string?)null);

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockTenantService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockMultiTenancyOptions.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerName.Should().Be("Home Server"); // Default name
    }

    #endregion
}
