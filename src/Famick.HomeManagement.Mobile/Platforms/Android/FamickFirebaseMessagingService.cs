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

        // Standard notification display
        var notification = message.GetNotification();
        var title = notification?.Title ?? "Famick Home";
        var body = notification?.Body ?? "";

        // Extract deep link from data payload
        string? deepLink = null;
        message.Data?.TryGetValue("deepLink", out deepLink);

        ShowLocalNotification(title, body, deepLink);
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

    [System.Runtime.Versioning.SupportedOSPlatform("android23.0")]
    private void ShowLocalNotification(string title, string body, string? deepLink)
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

        var notificationManager = NotificationManagerCompat.From(context);
        notificationManager.Notify(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().GetHashCode(), builder.Build());
    }
}
