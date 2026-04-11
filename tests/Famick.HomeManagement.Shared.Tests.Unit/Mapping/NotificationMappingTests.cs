using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Mapping;

public class NotificationMappingTests
{
    private readonly IMapper _mapper;

    public NotificationMappingTests()
    {
        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddProfile<NotificationMappingProfile>();
        }, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
        // Validation skipped: profiles are tested in isolation
        _mapper = config.CreateMapper();
    }

    [Fact]
    public void Notification_To_NotificationDto_MapsAllProperties()
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Type = MessageType.Expiry,
            Title = "Test Notification",
            Summary = "Test summary",
            DeepLinkUrl = "/test/link",
            IsRead = true,
            CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc)
        };

        var dto = _mapper.Map<NotificationDto>(notification);

        dto.Id.Should().Be(notification.Id);
        dto.Type.Should().Be(MessageType.Expiry);
        dto.Title.Should().Be("Test Notification");
        dto.Summary.Should().Be("Test summary");
        dto.DeepLinkUrl.Should().Be("/test/link");
        dto.IsRead.Should().BeTrue();
        dto.CreatedAt.Should().Be(notification.CreatedAt);
    }

    [Fact]
    public void Notification_To_NotificationDto_HandlesNullDeepLink()
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            Title = "Test",
            Summary = "Test",
            DeepLinkUrl = null
        };

        var dto = _mapper.Map<NotificationDto>(notification);

        dto.DeepLinkUrl.Should().BeNull();
    }

    [Fact]
    public void UserDeviceToken_To_DeviceTokenDto_MapsAllProperties()
    {
        var token = new UserDeviceToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Platform = DevicePlatform.iOS,
            CreatedAt = DateTime.UtcNow
        };

        var dto = _mapper.Map<DeviceTokenDto>(token);

        dto.Id.Should().Be(token.Id);
        dto.Platform.Should().Be(DevicePlatform.iOS);
        dto.CreatedAt.Should().Be(token.CreatedAt);
    }
}
