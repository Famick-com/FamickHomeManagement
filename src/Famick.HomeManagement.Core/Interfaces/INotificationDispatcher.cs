using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Dispatches a notification item through a specific channel (email, push, in-app).
/// The background service calls all registered dispatchers for each notification item.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches a notification item to the user through this channel
    /// </summary>
    /// <param name="item">The notification item to dispatch</param>
    /// <param name="preference">The user's preference for this notification type</param>
    /// <param name="user">The target user</param>
    /// <param name="tenantId">The tenant ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DispatchAsync(
        NotificationItem item,
        NotificationPreference preference,
        User user,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
