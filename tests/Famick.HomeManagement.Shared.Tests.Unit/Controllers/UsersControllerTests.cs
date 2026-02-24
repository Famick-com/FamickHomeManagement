using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Web.Shared.Controllers.v1;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Unit tests for UsersController focusing on welcome email functionality
/// </summary>
public class UsersControllerTests
{
    private readonly Mock<IUserManagementService> _mockUserManagementService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly UsersController _controller;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public UsersControllerTests()
    {
        _mockUserManagementService = new Mock<IUserManagementService>();
        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        var logger = new Mock<ILogger<UsersController>>();

        _controller = new UsersController(
            _mockUserManagementService.Object,
            _mockTenantProvider.Object,
            logger.Object);

        // Set up HttpContext with Request
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("app.famick.com");
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    #region Create User Tests

    [Fact]
    public async Task Create_PassesBaseUrlToService()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "newuser@example.com",
            FirstName = "John",
            LastName = "Doe",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = true
        };

        var response = new CreateUserResponse
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            WelcomeEmailSent = true
        };

        _mockUserManagementService
            .Setup(s => s.CreateUserAsync(
                It.IsAny<CreateUserRequest>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        await _controller.Create(request, CancellationToken.None);

        // Assert - verify service was called with correct baseUrl
        _mockUserManagementService.Verify(
            s => s.CreateUserAsync(
                request,
                "https://app.famick.com",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_WithSendWelcomeEmailTrue_ReturnsWelcomeEmailSentTrue()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "newuser@example.com",
            FirstName = "John",
            LastName = "Doe",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = true
        };

        var response = new CreateUserResponse
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            WelcomeEmailSent = true
        };

        _mockUserManagementService
            .Setup(s => s.CreateUserAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        var returnedResponse = createdResult.Value.Should().BeAssignableTo<CreateUserResponse>().Subject;
        returnedResponse.WelcomeEmailSent.Should().BeTrue();
    }

    [Fact]
    public async Task Create_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "",
            FirstName = "John",
            LastName = "Doe",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = false
        };

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithEmptyFirstName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user@example.com",
            FirstName = "",
            LastName = "Doe",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = false
        };

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_WithNoRoles_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user@example.com",
            FirstName = "John",
            LastName = "Doe",
            Roles = new List<Role>(),
            SendWelcomeEmail = false
        };

        // Act
        var result = await _controller.Create(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_HttpSchemeIncludedInBaseUrl()
    {
        // Arrange - Set up HTTP scheme (not HTTPS)
        _controller.ControllerContext.HttpContext.Request.Scheme = "http";
        _controller.ControllerContext.HttpContext.Request.Host = new HostString("localhost:5000");

        var request = new CreateUserRequest
        {
            Email = "newuser@example.com",
            FirstName = "John",
            LastName = "Doe",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = true
        };

        var response = new CreateUserResponse
        {
            UserId = Guid.NewGuid(),
            Email = request.Email,
            WelcomeEmailSent = true
        };

        _mockUserManagementService
            .Setup(s => s.CreateUserAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        await _controller.Create(request, CancellationToken.None);

        // Assert - verify HTTP scheme is used
        _mockUserManagementService.Verify(
            s => s.CreateUserAsync(
                request,
                "http://localhost:5000",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
