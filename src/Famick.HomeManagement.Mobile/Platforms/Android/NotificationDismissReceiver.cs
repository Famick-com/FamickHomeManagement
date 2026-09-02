using Android.App;
using Android.Content;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Fires when the user dismisses (swipes away / clears) a Famick notification that carried a server
/// notification id — set as the notification's delete intent. Marks that notification read so the
/// unread count and app-icon badge stay in sync without the user opening the app.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false)]
public class NotificationDismissReceiver : BroadcastReceiver
{
    internal const string ExtraNotificationId = "serverNotificationId";

    public override void OnReceive(Context? context, Intent? intent)
    {
        var notificationId = intent?.GetStringExtra(ExtraNotificationId);
        if (string.IsNullOrEmpty(notificationId))
            return;

        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                await NotificationActionHelper.MarkReadAsync(notificationId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NotificationDismissReceiver] Failed: {ex.Message}");
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
