using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Messaging.Services;

/// <summary>
/// Creates in-app notifications from rendered messages.
/// </summary>
public class InAppMessageTransport : IMessageTransport
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<InAppMessageTransport> _logger;

    public TransportChannel Channel => TransportChannel.InApp;

    public InAppMessageTransport(
        INotificationService notificationService,
        ILogger<InAppMessageTransport> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task SendAsync(RenderedMessage message, CancellationToken cancellationToken = default)
    {
        if (!message.UserId.HasValue || !message.TenantId.HasValue)
        {
            _logger.LogWarning("Cannot create in-app notification: missing UserId or TenantId for type {Type}", message.Type);
            return;
        }

        await _notificationService.CreateNotificationAsync(
            message.UserId.Value,
            message.TenantId.Value,
            message.Type,
            message.InAppTitle ?? message.Subject ?? message.Type.ToString(),
            message.InAppSummary ?? message.TextBody ?? string.Empty,
            message.DeepLinkUrl,
            message.ContentHash,
            cancellationToken);
    }
}
