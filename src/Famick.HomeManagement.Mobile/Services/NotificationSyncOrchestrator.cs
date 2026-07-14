using System.Globalization;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Coordinates the offline notification engine. Bulk-fetches upcoming reminders from the server while
/// online, diffs them against the locally scheduled set, and hands new/changed ones to the native
/// <see cref="INotificationScheduler"/> so they fire offline — the "download once, alert anytime"
/// strategy. Mirrors <see cref="CalendarSyncOrchestrator"/>'s enable/interval gating.
///
/// Runs in <b>both</b> cloud and self-hosted modes: the on-device scheduler is the single path that
/// displays scheduled reminders. Cloud additionally sends silent <c>reminderSync</c> pushes to keep
/// the local cache fresh in real time; self-hosted relies on the periodic prefetch. Cloud visible
/// push (APNs/FCM) is used only for event-driven notifications, so there is no double-delivery.
/// </summary>
public class NotificationSyncOrchestrator
{
    // Prefetch horizon. Long enough that iOS background-refresh gaps (and force-close, which stops
    // BGTask entirely) don't leave the user without near-term reminders between foregrounds.
    private const int PrefetchDays = 14;

    // Cap on how many reminders we keep scheduled at once. Bounded by iOS's hard 64-pending limit;
    // we keep the soonest ones and let later ones roll in on subsequent syncs as nearer ones fire.
    // Applied on both platforms so the local store exactly mirrors what is scheduled with the OS.
    private const int MaxScheduled = 60;

    private const string EnabledKey = "ReminderSyncEnabled";
    private const string LastSyncedKey = "ReminderSyncLastSyncedAt";

    private readonly ShoppingApiClient _apiClient;
    private readonly OfflineStorageService _storage;
    private readonly INotificationScheduler _scheduler;

    public NotificationSyncOrchestrator(
        ShoppingApiClient apiClient,
        OfflineStorageService storage,
        INotificationScheduler scheduler)
    {
        _apiClient = apiClient;
        _storage = storage;
        _scheduler = scheduler;
    }

    /// <summary>
    /// Whether offline reminders are enabled. Defaults on in cloud mode (the on-device scheduler is
    /// the primary delivery path there) and off in self-hosted mode (opt-in); an explicit user choice
    /// via the Settings toggle overrides the default.
    /// </summary>
    public static bool IsEnabled
    {
        get => Preferences.Get(EnabledKey, new ApiSettings().IsCloudServer());
        set => Preferences.Set(EnabledKey, value);
    }

    /// <summary>When the last successful prefetch completed.</summary>
    public static DateTime? LastSyncedAt
    {
        get
        {
            var str = Preferences.Get(LastSyncedKey, null as string);
            return str != null ? DateTime.Parse(str, null, DateTimeStyles.RoundtripKind) : null;
        }
    }

    /// <summary>Whether a prefetch should run given the minimum interval and enabled state.</summary>
    public static bool ShouldSync(TimeSpan minInterval)
    {
        if (!IsEnabled) return false;
        var last = LastSyncedAt;
        return last == null || DateTime.UtcNow - last.Value > minInterval;
    }

    /// <summary>
    /// Fetches the upcoming-reminder feed and reconciles it with the device's scheduled notifications.
    /// No-ops (leaving existing schedules intact) when disabled, unsupported, or offline.
    /// </summary>
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || !_scheduler.IsSupported)
            return;

        if (!await _scheduler.RequestPermissionAsync())
        {
            Console.WriteLine("[NotificationSync] Notification permission not granted; skipping");
            return;
        }

        var feedResult = await _apiClient.GetUpcomingRemindersAsync(PrefetchDays);
        if (!feedResult.Success || feedResult.Data == null)
        {
            // Offline or server error — keep whatever is already scheduled locally.
            Console.WriteLine($"[NotificationSync] Fetch failed: {feedResult.ErrorMessage}");
            return;
        }

        var now = DateTime.UtcNow;
        var feed = feedResult.Data
            .Where(r => r.FireAtUtc > now && !string.IsNullOrEmpty(r.Key))
            .GroupBy(r => r.Key)
            .Select(g => g.First())
            .OrderBy(r => r.FireAtUtc)
            .Take(MaxScheduled)  // keep the soonest; later ones roll in as these fire
            .ToList();

        var existing = (await _storage.GetScheduledRemindersAsync())
            .ToDictionary(r => r.Key, r => r);
        var feedKeys = feed.Select(r => r.Key).ToHashSet();

        // Cancel reminders that are no longer in the feed.
        foreach (var stale in existing.Values.Where(r => !feedKeys.Contains(r.Key)))
        {
            await _scheduler.CancelAsync(stale.Key);
            await _storage.DeleteScheduledReminderAsync(stale.Key);
        }

        // Schedule new or content-changed reminders.
        var toSchedule = new List<LocalReminder>();
        foreach (var item in feed)
        {
            if (existing.TryGetValue(item.Key, out var prior) && prior.ServerHash == item.ContentHash)
                continue; // unchanged — already scheduled

            toSchedule.Add(new LocalReminder(item.Key, item.FireAtUtc, item.Title, item.Body, item.DeepLinkUrl));
            await _storage.UpsertScheduledReminderAsync(new ScheduledReminder
            {
                Key = item.Key,
                FireAtUtc = item.FireAtUtc,
                Type = item.Type,
                Title = item.Title,
                Body = item.Body,
                DeepLink = item.DeepLinkUrl,
                ServerHash = item.ContentHash
            });
        }

        if (toSchedule.Count > 0)
            await _scheduler.ScheduleAsync(toSchedule, cancellationToken);

        // Drop reminders that have already fired from the local mirror.
        await _storage.DeletePastRemindersAsync(now);

        Preferences.Set(LastSyncedKey, DateTime.UtcNow.ToString("O"));
        Console.WriteLine($"[NotificationSync] Reconciled {feed.Count} reminder(s); {toSchedule.Count} (re)scheduled");
    }

    /// <summary>
    /// Re-arms all still-future reminders from the local store without hitting the network. Used
    /// after an Android reboot, where AlarmManager alarms are cleared but the store survives.
    /// </summary>
    public async Task RearmFromStoreAsync()
    {
        if (!IsEnabled || !_scheduler.IsSupported)
            return;

        var now = DateTime.UtcNow;
        await _storage.DeletePastRemindersAsync(now);

        var pending = (await _storage.GetScheduledRemindersAsync())
            .Where(r => r.FireAtUtc > now)
            .OrderBy(r => r.FireAtUtc)
            .Take(MaxScheduled)
            .Select(r => new LocalReminder(r.Key, r.FireAtUtc, r.Title, r.Body, r.DeepLink))
            .ToList();

        if (pending.Count > 0)
            await _scheduler.ScheduleAsync(pending);
    }

    /// <summary>Cancels every scheduled reminder and clears the local store (used when disabling).</summary>
    public async Task ClearAllAsync()
    {
        foreach (var reminder in await _storage.GetScheduledRemindersAsync())
            await _scheduler.CancelAsync(reminder.Key);

        await _scheduler.CancelAllAsync();
        await _storage.ClearScheduledRemindersAsync();
    }
}
