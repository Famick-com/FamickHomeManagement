using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Interfaces;
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
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<SetupApiController>> _mockLogger;

    public SetupApiControllerTests()
    {
        _mockSetupService = new Mock<ISetupService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<SetupApiController>>();
    }

    private SetupApiController CreateController(string? publicUrl = null, string? serverName = null)
    {
        // Setup configuration
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns(publicUrl);
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns(serverName ?? "Test Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

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
    public void GetMobileAppDeepLink_WithConfiguredUrl_ReturnsDeepLinkAndSetupPageUrl()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "My Home Server");

        // Act
        var result = controller.GetMobileAppDeepLink();

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
        response.SetupPageUrl.Should().Contain("https://home.example.com/app-setup.html?url=");
        response.SetupPageUrl.Should().Contain("name=My+Home+Server");
    }

    [Fact]
    public void GetMobileAppDeepLink_WithoutConfiguredUrl_FallsBackToRequestHost()
    {
        // Arrange
        var controller = CreateController(publicUrl: null, serverName: "Test Server");

        // Act
        var result = controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://localhost:5001");
        response.ServerName.Should().Be("Test Server");
    }

    [Fact]
    public void GetMobileAppDeepLink_WithForwardedHeaders_UsesForwardedHost()
    {
        // Arrange
        var controller = CreateController(publicUrl: null, serverName: "Proxied Server");

        // Add forwarded headers
        controller.HttpContext.Request.Headers["X-Forwarded-Host"] = new StringValues("proxy.example.com");
        controller.HttpContext.Request.Headers["X-Forwarded-Proto"] = new StringValues("https");

        // Act
        var result = controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://proxy.example.com");
    }

    [Fact]
    public void GetMobileAppDeepLink_UrlEncodesSpecialCharacters()
    {
        // Arrange
        var controller = CreateController(
            publicUrl: "https://home.example.com:8443",
            serverName: "John's Home & Kitchen");

        // Act
        var result = controller.GetMobileAppDeepLink();

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
    public void GetMobileAppConfig_WithConfiguredUrl_ReturnsIsEnabledAndIsConfiguredTrue()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act
        var result = controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.IsEnabled.Should().BeTrue();
        response.IsConfigured.Should().BeTrue();
        response.ServerUrl.Should().Be("https://home.example.com");
        response.ServerName.Should().Be("Home Server");
        response.DeepLinkScheme.Should().Be("famick");
        response.DeepLinkHost.Should().Be("setup");
    }

    [Fact]
    public void GetMobileAppConfig_WithoutConfiguredUrl_StillReturnsIsConfiguredTrue()
    {
        // Arrange - Even without PublicUrl config, falls back to request host
        var controller = CreateController(publicUrl: null, serverName: "Default Server");

        // Act
        var result = controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        // Falls back to request host, so still configured
        response.IsConfigured.Should().BeTrue();
        response.ServerUrl.Should().Be("https://localhost:5001");
    }

    [Fact]
    public void GetMobileAppConfig_ReturnsCorrectDeepLinkScheme()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com");

        // Act
        var result = controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.DeepLinkScheme.Should().Be("famick");
        response.DeepLinkHost.Should().Be("setup");
    }

    [Fact]
    public void GetMobileAppConfig_WhenDisabled_ReturnsIsEnabledFalse()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:Enabled"]).Returns("false");
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns("Home Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = controller.GetMobileAppConfig();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppConfigResponse>().Subject;

        response.IsEnabled.Should().BeFalse();
        response.IsConfigured.Should().BeFalse(); // IsConfigured requires IsEnabled
    }

    [Fact]
    public void GetMobileAppQrCode_WhenDisabled_ReturnsNotFound()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:Enabled"]).Returns("false");
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns("Home Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = controller.GetMobileAppQrCode();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetMobileAppDeepLink_WhenDisabled_ReturnsNotFound()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:Enabled"]).Returns("false");
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns("Home Server");

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = controller.GetMobileAppDeepLink();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region GetMobileAppQrCode Tests

    [Fact]
    public void GetMobileAppQrCode_WithConfiguredUrl_ReturnsPngFile()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act
        var result = controller.GetMobileAppQrCode();

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
    public void GetMobileAppQrCode_WithCustomPixelsPerModule_GeneratesDifferentSize()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act
        var smallResult = controller.GetMobileAppQrCode(pixelsPerModule: 5);
        var largeResult = controller.GetMobileAppQrCode(pixelsPerModule: 20);

        // Assert
        var smallFile = smallResult.Should().BeOfType<FileContentResult>().Subject;
        var largeFile = largeResult.Should().BeOfType<FileContentResult>().Subject;

        // Larger pixels per module should produce larger file (more image data)
        largeFile.FileContents.Length.Should().BeGreaterThan(smallFile.FileContents.Length);
    }

    [Fact]
    public void GetMobileAppQrCode_WithUseLandingPageTrue_GeneratesQrWithLandingPageUrl()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act - Default is useLandingPage: true
        var result = controller.GetMobileAppQrCode(pixelsPerModule: 10, useLandingPage: true);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    [Fact]
    public void GetMobileAppQrCode_WithUseLandingPageFalse_GeneratesQrWithDirectDeepLink()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com", serverName: "Home Server");

        // Act - Direct deep link QR
        var result = controller.GetMobileAppQrCode(pixelsPerModule: 10, useLandingPage: false);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        fileResult.FileContents.Should().NotBeEmpty();
    }

    #endregion

    #region URL Normalization Tests

    [Fact]
    public void GetMobileAppDeepLink_TrimsTrailingSlash()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://home.example.com/", serverName: "Test");

        // Act
        var result = controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerUrl.Should().Be("https://home.example.com");
        response.ServerUrl.Should().NotEndWith("/");
    }

    [Fact]
    public void GetMobileAppDeepLink_UsesConfiguredUrlOverForwardedHeaders()
    {
        // Arrange
        var controller = CreateController(publicUrl: "https://configured.example.com", serverName: "Test");

        // Add forwarded headers
        controller.HttpContext.Request.Headers["X-Forwarded-Host"] = new StringValues("proxy.example.com");
        controller.HttpContext.Request.Headers["X-Forwarded-Proto"] = new StringValues("https");

        // Act
        var result = controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        // Should use configured URL, not forwarded headers
        response.ServerUrl.Should().Be("https://configured.example.com");
    }

    #endregion

    #region Default Server Name Tests

    [Fact]
    public void GetMobileAppDeepLink_WithoutServerName_UsesDefaultName()
    {
        // Arrange
        _mockConfiguration.Setup(c => c["MobileAppSetup:PublicUrl"]).Returns("https://home.example.com");
        _mockConfiguration.Setup(c => c["MobileAppSetup:ServerName"]).Returns((string?)null);

        var controller = new SetupApiController(
            _mockSetupService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost", 5001);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = controller.GetMobileAppDeepLink();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeAssignableTo<MobileAppSetupResponse>().Subject;

        response.ServerName.Should().Be("Home Server"); // Default name
    }

    #endregion
}
