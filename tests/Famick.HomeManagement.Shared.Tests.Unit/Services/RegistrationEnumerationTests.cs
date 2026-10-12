using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Starting a signup must not reveal whether an address already has an account: the answer is
/// the same, an email goes out either way, and a failed send changes nothing the caller can see.
/// </summary>
public class RegistrationEnumerationTests
{
    private const string BaseUrl = "https://app.famick.com";
    private const string ExistingEmail = "resident@example.com";
    private const string NewEmail = "newcomer@example.com";

    private readonly HomeManagementDbContext _db = new(
        new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"registration-enumeration-{Guid.NewGuid()}")
            .Options);

    private readonly Mock<IMessageService> _messages = new();

    public RegistrationEnumerationTests()
    {
        _db.Users.Add(new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Email = ExistingEmail,
            Username = ExistingEmail,
            FirstName = "Resident",
            LastName = "Example",
            PasswordHash = "not-a-real-hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        _db.SaveChanges();
    }

    private RegistrationService BuildSut() => new(
        _db,
        Mock.Of<IEmailService>(),
        _messages.Object,
        Mock.Of<IPasswordHasher>(),
        Mock.Of<IPasskeyService>(),
        Mock.Of<ITokenService>(),
        new ConfigurationBuilder().Build(),
        NullLogger<RegistrationService>.Instance);

    private Task<StartRegistrationResponse> StartAsync(string email) =>
        BuildSut().StartRegistrationAsync(
            new StartRegistrationRequest { HouseholdName = "The Examples", Email = email },
            ipAddress: "203.0.113.7",
            deviceInfo: "test",
            baseUrl: BaseUrl,
            webVerificationPath: "/verify-email");

    [Fact]
    public async Task Start_ExistingAndNewAddress_GetTheSameAnswer()
    {
        var existing = await StartAsync(ExistingEmail);
        var fresh = await StartAsync(NewEmail);

        existing.Success.Should().BeTrue();
        fresh.Success.Should().BeTrue();
        existing.Message.Should().Be(RegistrationService.StartedMessage);
        fresh.Message.Should().Be(existing.Message);
    }

    [Fact]
    public async Task Start_ExistingAddress_EmailsTheOwnerToSignIn()
    {
        AccountAlreadyExistsData? sent = null;
        _messages
            .Setup(m => m.SendTransactionalAsync(
                ExistingEmail, MessageType.AccountAlreadyExists, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()))
            .Callback((string _, MessageType _, IMessageData data, CancellationToken _) =>
                sent = (AccountAlreadyExistsData)data)
            .Returns(Task.CompletedTask);

        await StartAsync(ExistingEmail);

        sent.Should().NotBeNull();
        sent!.LoginLink.Should().Be($"{BaseUrl}/login");
        sent.ForgotPasswordLink.Should().Be($"{BaseUrl}/forgot-password");
        _messages.Verify(m => m.SendTransactionalAsync(
            It.IsAny<string>(), MessageType.EmailVerification, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()),
            Times.Never);
        (await _db.EmailVerificationTokens.AnyAsync()).Should().BeFalse("no signup is started for a registered address");
    }

    [Fact]
    public async Task Start_NewAddress_SendsTheVerificationEmail()
    {
        await StartAsync(NewEmail);

        _messages.Verify(m => m.SendTransactionalAsync(
            NewEmail, MessageType.EmailVerification, It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(ExistingEmail)]
    [InlineData(NewEmail)]
    public async Task Start_WhenTheEmailFailsToSend_StillAnswersTheSame(string email)
    {
        _messages
            .Setup(m => m.SendTransactionalAsync(
                It.IsAny<string>(), It.IsAny<MessageType>(), It.IsAny<IMessageData>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SES is down"));

        var result = await StartAsync(email);

        result.Success.Should().BeTrue();
        result.Message.Should().Be(RegistrationService.StartedMessage);
    }

    [Fact]
    public async Task Resend_PendingAndUnknownAddress_GetTheSameAnswer()
    {
        await StartAsync(NewEmail);
        var sut = BuildSut();

        var pending = await sut.ResendVerificationEmailAsync(NewEmail, BaseUrl, "/verify-email");
        var unknown = await sut.ResendVerificationEmailAsync("nobody@example.com", BaseUrl, "/verify-email");

        pending.Message.Should().Be(RegistrationService.ResentMessage);
        unknown.Message.Should().Be(pending.Message);
        unknown.Success.Should().Be(pending.Success);
    }
}
