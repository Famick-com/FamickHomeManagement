using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.Configuration;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Messaging.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Messaging.Tests.Unit.Services;

public class MessageServiceTests
{
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IUnsubscribeTokenService> _mockUnsubscribeTokenService;
    private readonly Mock<IMessageRecipientResolver> _mockRecipientResolver;
    private readonly Mock<IMessageTransport> _mockEmailTransport;
    private readonly Mock<IMessageTransport> _mockSmsTransport;
    private readonly Mock<IMessageTransport> _mockInAppTransport;
    private readonly MessageService _service;

    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _tenantId = Guid.NewGuid();

    public MessageServiceTests()
    {
        var renderer = new StubbleTemplateRenderer(NullLogger<StubbleTemplateRenderer>.Instance);

        _mockNotificationService = new Mock<INotificationService>();
        _mockUnsubscribeTokenService = new Mock<IUnsubscribeTokenService>();
        _mockRecipientResolver = new Mock<IMessageRecipientResolver>();

        _mockEmailTransport = new Mock<IMessageTransport>();
        _mockEmailTransport.Setup(t => t.Channel).Returns(TransportChannel.EmailHtml);

        _mockSmsTransport = new Mock<IMessageTransport>();
        _mockSmsTransport.Setup(t => t.Channel).Returns(TransportChannel.Sms);

        _mockInAppTransport = new Mock<IMessageTransport>();
        _mockInAppTransport.Setup(t => t.Channel).Returns(TransportChannel.InApp);

        var transports = new[] { _mockEmailTransport.Object, _mockSmsTransport.Object, _mockInAppTransport.Object };

        var complianceSettings = Options.Create(new ComplianceSettings
        {
            CompanyName = "Test Co",
            PhysicalAddress = "123 Main St",
            UnsubscribeBaseUrl = "https://app.test.com"
        });

        _mockUnsubscribeTokenService
            .Setup(s => s.GenerateToken(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<MessageType>()))
            .Returns("test-token");

        _service = new MessageService(
            renderer,
            transports,
            _mockNotificationService.Object,
            _mockUnsubscribeTokenService.Object,
            _mockRecipientResolver.Object,
            complianceSettings,
            NullLogger<MessageService>.Instance);
    }

    private void SetupActiveRecipient()
    {
        _mockRecipientResolver
            .Setup(r => r.ResolveAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageRecipient(
                _userId, "user@test.com", "+15551234567", "John", "Doe", _tenantId, true));
    }

    private void SetupPreferences(bool emailEnabled = true, bool smsEnabled = false, bool inAppEnabled = true)
    {
        _mockNotificationService
            .Setup(s => s.GetPreferencesAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<NotificationPreferenceDto>
            {
                new()
                {
                    MessageType = MessageType.Expiry,
                    EmailEnabled = emailEnabled,
                    SmsEnabled = smsEnabled,
                    InAppEnabled = inAppEnabled,
                    PushEnabled = true
                }
            });
    }

    [Fact]
    public async Task SendAsync_UserNotFound_DoesNotSend()
    {
        _mockRecipientResolver
            .Setup(r => r.ResolveAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MessageRecipient?)null);

        var data = new ExpiryData { Title = "Test" };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        _mockEmailTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_InactiveUser_DoesNotSend()
    {
        _mockRecipientResolver
            .Setup(r => r.ResolveAsync(_userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageRecipient(_userId, "user@test.com", null, "John", "Doe", _tenantId, false));

        var data = new ExpiryData { Title = "Test" };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        _mockEmailTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_EmailDisabled_DoesNotSendEmail()
    {
        SetupActiveRecipient();
        SetupPreferences(emailEnabled: false, inAppEnabled: true);

        var data = new ExpiryData
        {
            Title = "2 items expiring",
            Summary = "1 expired, 1 expiring",
            ExpiredCount = 1,
            ExpiringSoonCount = 1,
            ExpiringItems = [new() { ProductName = "Milk", ExpiryDate = "2026-04-01", LocationName = "Fridge", IsExpired = true }]
        };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        _mockEmailTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockInAppTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_SmsEnabledWithPhoneNumber_SendsSms()
    {
        SetupActiveRecipient();
        SetupPreferences(smsEnabled: true);

        var data = new ExpiryData
        {
            Title = "Test",
            Summary = "Test",
            ExpiredCount = 1,
            ExpiringSoonCount = 0,
            ExpiringItems = [new() { ProductName = "Milk", ExpiryDate = "2026-04-01", LocationName = "Fridge", IsExpired = true }]
        };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        _mockSmsTransport.Verify(t => t.SendAsync(
            It.Is<RenderedMessage>(m => !string.IsNullOrEmpty(m.SmsBody)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_SmsDisabledByDefault_DoesNotSendSms()
    {
        SetupActiveRecipient();
        SetupPreferences(smsEnabled: false);

        var data = new ExpiryData { Title = "Test", Summary = "Test", ExpiredCount = 1, ExpiringSoonCount = 0 };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        _mockSmsTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_NotificationMessage_IncludesUnsubscribeUrl()
    {
        SetupActiveRecipient();
        SetupPreferences();

        var data = new ExpiryData
        {
            Title = "Test",
            Summary = "Test",
            ExpiredCount = 1,
            ExpiringSoonCount = 0,
            ExpiringItems = [new() { ProductName = "Milk", ExpiryDate = "2026-04-01", LocationName = "Fridge", IsExpired = true }]
        };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        _mockEmailTransport.Verify(t => t.SendAsync(
            It.Is<RenderedMessage>(m =>
                m.UnsubscribeUrl != null &&
                m.UnsubscribeUrl.Contains("test-token") &&
                m.HtmlBody!.Contains("Unsubscribe")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_ContinuesToNextTransport()
    {
        SetupActiveRecipient();
        SetupPreferences(inAppEnabled: true);

        _mockEmailTransport
            .Setup(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Email failed"));

        var data = new ExpiryData { Title = "Test", Summary = "Test", ExpiredCount = 1, ExpiringSoonCount = 0 };

        await _service.SendAsync(_userId, MessageType.Expiry, data);

        // Email threw, but in-app should still be called
        _mockInAppTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTransactionalAsync_SendsEmailOnly()
    {
        var data = new PasswordResetData
        {
            UserName = "Jane",
            ResetLink = "https://app.test.com/reset?token=abc"
        };

        await _service.SendTransactionalAsync("jane@test.com", MessageType.PasswordReset, data);

        _mockEmailTransport.Verify(t => t.SendAsync(
            It.Is<RenderedMessage>(m =>
                m.ToEmail == "jane@test.com" &&
                m.HtmlBody!.Contains("Jane") &&
                m.UnsubscribeUrl == null),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockSmsTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockInAppTransport.Verify(t => t.SendAsync(It.IsAny<RenderedMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendTransactionalAsync_NoComplianceFooterInHtml()
    {
        var data = new WelcomeData
        {
            UserName = "Jane",
            Email = "jane@test.com",
            TemporaryPassword = "temp123",
            LoginUrl = "https://app.test.com"
        };

        await _service.SendTransactionalAsync("jane@test.com", MessageType.Welcome, data);

        _mockEmailTransport.Verify(t => t.SendAsync(
            It.Is<RenderedMessage>(m => !m.HtmlBody!.Contains("Unsubscribe")),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
