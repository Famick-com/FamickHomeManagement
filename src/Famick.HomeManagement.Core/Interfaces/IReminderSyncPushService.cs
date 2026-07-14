namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Sends silent push notifications that tell a tenant's devices to refresh their locally-scheduled
/// reminders (the offline notification engine). Fired when reminder-relevant data changes (e.g. a
/// calendar event is created/edited/deleted) so the device re-pulls <c>/api/v1/notifications/upcoming</c>
/// and reschedules — giving cloud users real-time freshness without a visible push.
///
/// Cloud provides the real APNs/FCM silent-push implementation; self-hosted uses a no-op
/// (<c>NullReminderSyncPushService</c>) and relies on the periodic prefetch instead.
/// </summary>
public interface IReminderSyncPushService
{
    /// <summary>
    /// Silently nudges every device in the tenant to re-sync its scheduled reminders.
    /// </summary>
    Task NotifyRemindersChangedAsync(Guid tenantId, CancellationToken ct = default);
}
