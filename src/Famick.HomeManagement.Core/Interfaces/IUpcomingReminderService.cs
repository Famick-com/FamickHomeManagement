using Famick.HomeManagement.Core.DTOs.Notifications;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Produces future-dated reminders for a single user so a client can pre-schedule them as
/// local OS notifications. Unlike <see cref="INotificationEvaluator"/> (which produces
/// "fire now" items for the daily/calendar background jobs to dispatch through the message
/// pipeline), this projects reminders <b>forward</b> in time with an explicit fire timestamp.
///
/// It is the server half of the self-hosted offline notification engine: a self-hosted server
/// has no push transport, so the mobile app bulk-fetches these while it has connectivity and
/// hands them to the device to fire offline.
/// </summary>
public interface IUpcomingReminderService
{
    /// <summary>
    /// Returns the reminders that should fire for <paramref name="userId"/> within the next
    /// <paramref name="days"/> days, ordered by fire time. Respects the user's per-type
    /// <c>PushEnabled</c> preference (local reminders are the self-hosted stand-in for push).
    /// </summary>
    Task<IReadOnlyList<UpcomingReminderDto>> GetUpcomingAsync(
        Guid tenantId,
        Guid userId,
        int days,
        CancellationToken cancellationToken = default);
}
