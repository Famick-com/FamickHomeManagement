using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Unit tests for UserManagementService focusing on welcome email functionality
/// </summary>
public class UserManagementServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Mock<IPasswordHasher> _mockPasswordHasher;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<IMessageService> _mockMessageService;
    private readonly Mock<ITenantProvider> _mockTenantProvider;
    private readonly Mock<IContactService> _mockContactService;
    private readonly UserManagementService _service;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public UserManagementServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        _mockPasswordHasher = new Mock<IPasswordHasher>();
        _mockPasswordHasher
            .Setup(h => h.HashPassword(It.IsAny<string>()))
            .Returns("hashed_password");

        _mockEmailService = new Mock<IEmailService>();
        _mockEmailService
            .Setup(e => e.SendWelcomeEmailAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockTenantProvider = new Mock<ITenantProvider>();
        _mockTenantProvider.Setup(t => t.TenantId).Returns(_tenantId);

        _mockContactService = new Mock<IContactService>();

        var logger = new Mock<ILogger<UserManagementService>>();

        _mockMessageService = new Mock<IMessageService>();

        _service = new UserManagementService(
            _context,
            _mockPasswordHasher.Object,
            _mockEmailService.Object,
            _mockMessageService.Object,
            _mockTenantProvider.Object,
            _mockContactService.Object,
            logger.Object);

        // Create a tenant for tests
        _context.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Test Household",
            CreatedAt = DateTime.UtcNow
        });
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region CreateUserAsync Welcome Email Tests

    [Fact]
    public async Task CreateUserAsync_WithSendWelcomeEmail_PassesBaseUrlToEmailService()
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
        var baseUrl = "https://app.famick.com";

        // Act
        await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert - verify message service was called with correct data
        _mockMessageService.Verify(
            m => m.SendTransactionalAsync(
                request.Email,
                MessageType.Welcome,
                It.Is<WelcomeData>(d => d.LoginUrl == baseUrl && d.UserName == "John Doe"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WithoutSendWelcomeEmail_DoesNotCallEmailService()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "newuser@example.com",
            FirstName = "Jane",
            LastName = "Smith",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = false
        };
        var baseUrl = "https://app.famick.com";

        // Act
        await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert - verify message service was NOT called
        _mockMessageService.Verify(
            m => m.SendTransactionalAsync(
                It.IsAny<string>(),
                It.IsAny<MessageType>(),
                It.IsAny<IMessageData>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_WithSendWelcomeEmail_ReturnsWelcomeEmailSentTrue()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user1@example.com",
            FirstName = "Test",
            LastName = "User",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = true
        };
        var baseUrl = "https://app.famick.com";

        // Act
        var result = await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert
        result.WelcomeEmailSent.Should().BeTrue();
    }

    [Fact]
    public async Task CreateUserAsync_WithoutSendWelcomeEmail_ReturnsWelcomeEmailSentFalse()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user2@example.com",
            FirstName = "Test",
            LastName = "User",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = false
        };
        var baseUrl = "https://app.famick.com";

        // Act
        var result = await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert
        result.WelcomeEmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUserAsync_EmailServiceFails_ReturnsWelcomeEmailSentFalse()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user3@example.com",
            FirstName = "Test",
            LastName = "User",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = true
        };
        var baseUrl = "https://app.famick.com";

        // Setup message service to throw
        _mockMessageService
            .Setup(m => m.SendTransactionalAsync(
                It.IsAny<string>(),
                It.IsAny<MessageType>(),
                It.IsAny<IMessageData>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Email service unavailable"));

        // Act
        var result = await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert - user created but email not sent
        result.UserId.Should().NotBeEmpty();
        result.WelcomeEmailSent.Should().BeFalse();
    }

    [Fact]
    public async Task CreateUserAsync_WithProvidedPassword_UsesProvidedPassword()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user4@example.com",
            FirstName = "Test",
            LastName = "User",
            Password = "MySecurePassword123!",
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = true
        };
        var baseUrl = "https://app.famick.com";

        // Act
        await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert - verify password hasher was called with provided password
        _mockPasswordHasher.Verify(
            h => h.HashPassword("MySecurePassword123!"),
            Times.Once);

        // And message service was called with the provided password
        _mockMessageService.Verify(
            m => m.SendTransactionalAsync(
                request.Email,
                MessageType.Welcome,
                It.Is<WelcomeData>(d => d.TemporaryPassword == "MySecurePassword123!"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WithGeneratedPassword_ReturnsGeneratedPassword()
    {
        // Arrange
        var request = new CreateUserRequest
        {
            Email = "user5@example.com",
            FirstName = "Test",
            LastName = "User",
            Password = null, // No password provided - will be generated
            Roles = new List<Role> { Role.Editor },
            SendWelcomeEmail = false
        };
        var baseUrl = "https://app.famick.com";

        // Act
        var result = await _service.CreateUserAsync(request, baseUrl, CancellationToken.None);

        // Assert
        result.GeneratedPassword.Should().NotBeNullOrEmpty();
        result.GeneratedPassword!.Length.Should().Be(12); // GeneratedPasswordLength constant
    }

    #endregion
}
