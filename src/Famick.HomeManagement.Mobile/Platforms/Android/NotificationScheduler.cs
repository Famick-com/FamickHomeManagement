using Android.App;
using Android.Content;
using Android.OS;
using Famick.HomeManagement.Mobile.Services;
using Application = Android.App.Application;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android local-notification scheduler built on <see cref="AlarmManager"/>. Each reminder is an
/// exact alarm that broadcasts to <see cref="ReminderAlarmReceiver"/> at its fire time, which then
/// posts a notification — so reminders fire offline, and (unlike iOS) even after the app is
/// force-closed or the device is in Doze (via <c>SetExactAndAllowWhileIdle</c>).
///
/// Alarms do NOT survive a reboot; <see cref="BootReceiver"/> re-arms them from the local store.
/// Because Android exposes no "list pending alarms" API, the authoritative record of what is
/// scheduled lives in the SQLite store owned by <c>NotificationSyncOrchestrator</c>;
/// <see cref="GetScheduledKeysAsync"/> therefore returns empty and the orchestrator diffs against
/// the store instead.
/// </summary>
public class NotificationScheduler : INotificationScheduler
{
    internal const string ChannelId = "famick_reminders";
    internal const string ExtraKey = "reminderKey";
    internal const string ExtraTitle = "reminderTitle";
    internal const string ExtraBody = "reminderBody";
    internal const string ExtraDeepLink = "reminderDeepLink";

    public bool IsSupported => true;

    public async Task<bool> RequestPermissionAsync()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            return status == PermissionStatus.Granted;
        }
        return true;
    }

    public Task ScheduleAsync(IEnumerable<LocalReminder> items, CancellationToken cancellationToken = default)
    {
        var context = Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return Task.CompletedTask;

        var canExact = CanScheduleExact(alarmManager);
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.FireAtUtc <= now) continue;

            try
            {
                var pending = BuildAlarmPendingIntent(context, item, PendingIntentFlags.UpdateCurrent);
                if (pending == null) continue;

                // Force UTC kind — JSON/SQLite round-trips can drop it, and DateTimeOffset would
                // otherwise treat an Unspecified value as device-local time.
                var fireUtc = DateTime.SpecifyKind(item.FireAtUtc, DateTimeKind.Utc);
                var triggerAtMillis = new DateTimeOffset(fireUtc).ToUnixTimeMilliseconds();

                if (canExact)
                    alarmManager.SetExactAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
                else
                    // Exact alarms not permitted (Android 12+ without USE_EXACT_ALARM grant):
                    // fall back to an inexact idle alarm — it may fire late under Doze.
                    alarmManager.SetAndAllowWhileIdle(AlarmType.RtcWakeup, triggerAtMillis, pending);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificationScheduler.Android] Failed to schedule {item.Key}: {ex.Message}");
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetScheduledKeysAsync()
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task CancelAsync(string key)
    {
        var context = Application.Context;
        var alarmManager = (AlarmManager?)context.GetSystemService(Context.AlarmService);
        if (alarmManager == null) return Task.CompletedTask;

        // Recreate the same PendingIntent (NoCreate → null if it never existed) to cancel the alarm.
        var reminder = new LocalReminder(key, DateTime.UtcNow, "", "", null);
        var pending = BuildAlarmPendingIntent(context, reminder, PendingIntentFlags.NoCreate);
        if (pending != null)
        {
            alarmManager.Cancel(pending);
            pending.Cancel();
        }
        return Task.CompletedTask;
    }

    public Task CancelAllAsync()
    {
        // No enumeration API on Android — the orchestrator cancels each known key from the store.
        // Provided for interface completeness; a full wipe is driven key-by-key by the caller.
        return Task.CompletedTask;
    }

    private static bool CanScheduleExact(AlarmManager alarmManager)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            try { return alarmManager.CanScheduleExactAlarms(); }
            catch { return false; }
        }
        return true;
    }

    private static PendingIntent? BuildAlarmPendingIntent(Context context, LocalReminder item, PendingIntentFlags extraFlags)
    {
        var intent = new Intent(context, typeof(ReminderAlarmReceiver));
        intent.SetAction($"com.famick.homemanagement.REMINDER.{item.Key}");
        intent.PutExtra(ExtraKey, item.Key);
        intent.PutExtra(ExtraTitle, item.Title);
        intent.PutExtra(ExtraBody, item.Body);
        if (!string.IsNullOrEmpty(item.DeepLink))
            intent.PutExtra(ExtraDeepLink, item.DeepLink);

        var flags = extraFlags | PendingIntentFlags.Immutable;
        return PendingIntent.GetBroadcast(context, StableRequestCode(item.Key), intent, flags);
    }

    /// <summary>Deterministic (run-stable) request/notification id from a reminder key (FNV-1a).</summary>
    internal static int StableRequestCode(string key)
    {
        unchecked
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            foreach (var c in key)
            {
                hash ^= c;
                hash *= prime;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }
}
