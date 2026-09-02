namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// A single reminder to schedule as a native OS-level local notification.
/// </summary>
/// <param name="Key">Stable identifier (matches the server feed key) used to schedule/cancel/dedup.</param>
/// <param name="FireAtUtc">When the notification should fire (UTC).</param>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification body.</param>
/// <param name="DeepLink">Optional deep link to open when tapped.</param>
public record LocalReminder(string Key, DateTime FireAtUtc, string Title, string Body, string? DeepLink);

/// <summary>
/// Platform abstraction over the native local-notification scheduler. This is the client half of
/// the self-hosted offline notification engine: reminders fetched from the server are handed to the
/// OS (iOS <c>UNCalendarNotificationTrigger</c> / Android <c>AlarmManager</c>) so they fire locally
/// even with no network — replacing cloud push, which self-hosted servers cannot provide.
///
/// Implementations are registered per-platform in <c>MauiProgram</c>.
/// </summary>
public interface INotificationScheduler
{
    /// <summary>Whether local scheduling is available on this platform/OS version.</summary>
    bool IsSupported { get; }

    /// <summary>Requests the OS notification permission. Returns true if granted.</summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Schedules each reminder as a local notification keyed by <see cref="LocalReminder.Key"/>.
    /// Re-scheduling an existing key replaces it. Past reminders are ignored.
    /// </summary>
    Task ScheduleAsync(IEnumerable<LocalReminder> items, CancellationToken cancellationToken = default);

    /// <summary>Returns the keys of reminders currently scheduled with the OS.</summary>
    Task<IReadOnlyList<string>> GetScheduledKeysAsync();

    /// <summary>Cancels a single scheduled reminder by key.</summary>
    Task CancelAsync(string key);

    /// <summary>Cancels all reminders scheduled by this app.</summary>
    Task CancelAllAsync();
}
