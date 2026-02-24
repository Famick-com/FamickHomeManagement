using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Dispatches notifications to the in-app notification center by creating Notification entities.
/// </summary>
public class InAppNotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<InAppNotificationDispatcher> _logger;

    public InAppNotificationDispatcher(
        INotificationService notificationService,
        ILogger<InAppNotificationDispatcher> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task DispatchAsync(
        NotificationItem item,
        NotificationPreference preference,
        User user,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!preference.InAppEnabled)
        {
            _logger.LogDebug("In-app notifications disabled for user {UserId}, type {Type}", user.Id, item.Type);
            return;
        }

        await _notificationService.CreateNotificationAsync(
            item.UserId,
            tenantId,
            item.Type,
            item.Title,
            item.Summary,
            item.DeepLinkUrl,
            cancellationToken);

        _logger.LogDebug("In-app notification created for user {UserId}, type {Type}", user.Id, item.Type);
    }
}
