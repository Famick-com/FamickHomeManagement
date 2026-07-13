using Android.App;
using Android.Content;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Receives AlarmManager broadcasts scheduled by <see cref="NotificationScheduler"/> and posts the
/// reminder as a local notification. Fires even if the app is not running (BroadcastReceiver is
/// declared in the manifest / via this attribute).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class ReminderAlarmReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent == null) return;

        var key = intent.GetStringExtra(NotificationScheduler.ExtraKey) ?? "";
        var title = intent.GetStringExtra(NotificationScheduler.ExtraTitle) ?? "Famick Home";
        var body = intent.GetStringExtra(NotificationScheduler.ExtraBody) ?? "";
        var deepLink = intent.GetStringExtra(NotificationScheduler.ExtraDeepLink);

        var notificationId = string.IsNullOrEmpty(key)
            ? (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() & 0x7FFFFFFF)
            : NotificationScheduler.StableRequestCode(key);

        try
        {
            LocalNotificationPresenter.Show(
                context, notificationId, NotificationScheduler.ChannelId, title, body, deepLink);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ReminderAlarmReceiver] Failed to post notification: {ex.Message}");
        }
    }
}
