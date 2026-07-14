using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Famick.HomeManagement.Mobile.Services;
using Firebase.Messaging;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Handles incoming FCM messages and token refreshes.
/// Shows a local notification when a message arrives while the app is in the foreground.
/// Handles silent data-only messages for contact sync.
/// </summary>
[Service(Exported = false)]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class FamickFirebaseMessagingService : FirebaseMessagingService
{
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        PushTokenProvider.HandleTokenRefresh(token);
    }

    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        // Check for silent data-only actions (contact sync)
        string? action = null;
        message.Data?.TryGetValue("action", out action);

        if (action == "contactSync")
        {
            message.Data!.TryGetValue("contactId", out var contactId);
            if (Guid.TryParse(contactId, out var id))
                HandleContactSync(id);
            return;
        }

        if (action == "contactDeleted")
        {
            message.Data!.TryGetValue("contactId", out var contactId);
            if (Guid.TryParse(contactId, out var id))
                HandleContactDeleted(id);
            return;
        }

        if (action == "reminderSync")
        {
            HandleReminderSync();
            return;
        }

        // Standard notification display. Visible pushes are sent data-only (no FCM "notification"
        // block) so OnMessageReceived always fires and we can post the notification ourselves with a
        // delete intent — title/body therefore come from the data payload, not GetNotification().
        string? title = null, body = null, deepLink = null, notificationId = null;
        message.Data?.TryGetValue("title", out title);
        message.Data?.TryGetValue("body", out body);
        message.Data?.TryGetValue("deepLink", out deepLink);
        message.Data?.TryGetValue("notificationId", out notificationId);

        // Fall back to the notification block for any legacy notification-style messages.
        var notification = message.GetNotification();
        ShowLocalNotification(
            title ?? notification?.Title ?? "Famick Home",
            body ?? notification?.Body ?? "",
            deepLink,
            notificationId);
    }

    private static void HandleContactSync(Guid contactId)
    {
        Task.Run(async () =>
        {
            try
            {
                var orchestrator = IPlatformApplication.Current?.Services
                    .GetService<ContactSyncOrchestrator>();
                if (orchestrator != null)
                    await orchestrator.SyncSingleContactAsync(contactId);
            }
            catch { /* Non-critical */ }
        });
    }

    private static void HandleContactDeleted(Guid contactId)
    {
        Task.Run(async () =>
        {
            try
            {
                var orchestrator = IPlatformApplication.Current?.Services
                    .GetService<ContactSyncOrchestrator>();
                if (orchestrator != null)
                    await orchestrator.DeleteSingleContactAsync(contactId);
            }
            catch { /* Non-critical */ }
        });
    }

    private static void HandleReminderSync()
    {
        // Silent nudge: refresh locally-scheduled reminders after a server-side change.
        Task.Run(async () =>
        {
            try
            {
                var orchestrator = IPlatformApplication.Current?.Services
                    .GetService<NotificationSyncOrchestrator>();
                if (orchestrator != null)
                    await orchestrator.SyncAsync();
            }
            catch { /* Non-critical */ }
        });
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android23.0")]
    private void ShowLocalNotification(string title, string body, string? deepLink, string? notificationId = null)
    {
        var context = ApplicationContext;
        if (context == null) return;

        var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
        if (intent != null)
        {
            intent.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            if (!string.IsNullOrEmpty(deepLink))
            {
                intent.SetData(global::Android.Net.Uri.Parse(deepLink));
            }
        }

        var pendingIntent = PendingIntent.GetActivity(
            context, 0, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, "famick_default")
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault)
            .SetContentIntent(pendingIntent);

        // Detect dismissal: fire NotificationDismissReceiver (marks the server notification read).
        if (!string.IsNullOrEmpty(notificationId))
        {
            var dismissIntent = new Intent(context, typeof(NotificationDismissReceiver));
            dismissIntent.PutExtra(NotificationDismissReceiver.ExtraNotificationId, notificationId);
            var dismissPending = PendingIntent.GetBroadcast(
                context,
                NotificationScheduler.StableRequestCode(notificationId),
                dismissIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
            builder.SetDeleteIntent(dismissPending);
        }

        var notificationManager = NotificationManagerCompat.From(context);
        var postId = string.IsNullOrEmpty(notificationId)
            ? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().GetHashCode()
            : NotificationScheduler.StableRequestCode(notificationId);
        notificationManager.Notify(postId, builder.Build());
    }
}
