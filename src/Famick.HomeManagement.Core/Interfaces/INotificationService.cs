using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// CRUD operations for notifications, preferences, and device tokens
/// </summary>
public interface INotificationService
{
    #region Notifications

    /// <summary>
    /// Lists notifications for a user with optional read/unread filtering and pagination
    /// </summary>
    Task<NotificationListResponse> GetNotificationsAsync(
        Guid userId,
        NotificationListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of unread notifications for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a single notification as read
    /// </summary>
    Task MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks all notifications for a user as read
    /// </summary>
    Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Dismisses (soft-deletes) a notification
    /// </summary>
    Task DismissAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new in-app notification
    /// </summary>
    Task CreateNotificationAsync(
        Guid userId,
        Guid tenantId,
        NotificationType type,
        string title,
        string summary,
        string? deepLinkUrl = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a notification of the given type was already sent to the user today
    /// </summary>
    Task<bool> WasNotifiedTodayAsync(
        Guid userId,
        NotificationType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes notifications older than the retention period
    /// </summary>
    Task CleanupOldNotificationsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default);

    #endregion

    #region Preferences

    /// <summary>
    /// Gets notification preferences for a user, creating defaults if they don't exist
    /// </summary>
    Task<List<NotificationPreferenceDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates notification preferences for a user
    /// </summary>
    Task UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Disables email for a specific notification type (used by unsubscribe flow)
    /// </summary>
    Task DisableEmailForTypeAsync(
        Guid userId,
        Guid tenantId,
        NotificationType type,
        CancellationToken cancellationToken = default);

    #endregion

    #region Device Tokens

    /// <summary>
    /// Registers a push notification device token
    /// </summary>
    Task<DeviceTokenDto> RegisterDeviceTokenAsync(
        Guid userId,
        RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unregisters a push notification device token
    /// </summary>
    Task UnregisterDeviceTokenAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all device tokens for a user (used by push notification dispatcher)
    /// </summary>
    Task<List<Domain.Entities.UserDeviceToken>> GetDeviceTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    #endregion
}
