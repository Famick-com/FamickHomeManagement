using Famick.HomeManagement.Mobile.Services;
using Foundation;
using UserNotifications;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS local-notification scheduler built on <see cref="UNUserNotificationCenter"/>.
/// Each reminder becomes a one-shot <see cref="UNCalendarNotificationTrigger"/> keyed by its stable
/// <see cref="LocalReminder.Key"/>, so it fires offline at the scheduled time. Notification taps are
/// handled by the existing <see cref="ForegroundNotificationDelegate"/> (reads the deep link from
/// <c>UserInfo</c>).
/// </summary>
public class NotificationScheduler : INotificationScheduler
{
    // iOS caps pending local notifications at 64. Stay under it and let each foreground refresh
    // re-fill the window with the soonest reminders.
    private const int MaxPending = 60;

    // UserInfo key carrying the (possibly relative) deep link for a scheduled reminder.
    internal const string DeepLinkKey = "reminderDeepLink";

    public bool IsSupported => true;

    public async Task<bool> RequestPermissionAsync()
    {
        try
        {
            var (granted, _) = await UNUserNotificationCenter.Current.RequestAuthorizationAsync(
                UNAuthorizationOptions.Alert | UNAuthorizationOptions.Sound | UNAuthorizationOptions.Badge);
            return granted;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[NotificationScheduler] Permission request failed: {ex.Message}");
            return false;
        }
    }

    public async Task ScheduleAsync(IEnumerable<LocalReminder> items, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var ordered = items
            .Where(i => i.FireAtUtc > now)
            .OrderBy(i => i.FireAtUtc)
            .Take(MaxPending)
            .ToList();

        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var content = new UNMutableNotificationContent
                {
                    Title = item.Title,
                    Body = item.Body,
                    Sound = UNNotificationSound.Default
                };

                if (!string.IsNullOrEmpty(item.DeepLink))
                {
                    content.UserInfo = NSDictionary.FromObjectAndKey(
                        new NSString(item.DeepLink), new NSString(DeepLinkKey));
                }

                // Absolute one-shot: express the fire instant in device-local wall-clock components.
                // Force UTC kind first — JSON/SQLite round-trips can drop it, and ToLocalTime()
                // would then misinterpret the value.
                var local = DateTime.SpecifyKind(item.FireAtUtc, DateTimeKind.Utc).ToLocalTime();
                var components = new NSDateComponents
                {
                    Year = local.Year,
                    Month = local.Month,
                    Day = local.Day,
                    Hour = local.Hour,
                    Minute = local.Minute,
                    Second = local.Second
                };

                var trigger = UNCalendarNotificationTrigger.CreateTrigger(components, repeats: false);
                var request = UNNotificationRequest.FromIdentifier(item.Key, content, trigger);

                await UNUserNotificationCenter.Current.AddNotificationRequestAsync(request);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificationScheduler] Failed to schedule {item.Key}: {ex.Message}");
            }
        }
    }

    public async Task<IReadOnlyList<string>> GetScheduledKeysAsync()
    {
        var pending = await UNUserNotificationCenter.Current.GetPendingNotificationRequestsAsync();
        return pending.Select(p => p.Identifier).ToList();
    }

    public Task CancelAsync(string key)
    {
        UNUserNotificationCenter.Current.RemovePendingNotificationRequests(new[] { key });
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        UNUserNotificationCenter.Current.RemoveAllPendingNotificationRequests();
        return Task.CompletedTask;
    }
}
