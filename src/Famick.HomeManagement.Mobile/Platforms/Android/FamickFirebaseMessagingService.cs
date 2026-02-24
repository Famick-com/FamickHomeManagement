using Android.App;
using Android.Content;
using AndroidX.Core.App;
using Firebase.Messaging;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Handles incoming FCM messages and token refreshes.
/// Shows a local notification when a message arrives while the app is in the foreground.
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

        var notification = message.GetNotification();
        var title = notification?.Title ?? "Famick Home";
        var body = notification?.Body ?? "";

        // Extract deep link from data payload
        string? deepLink = null;
        message.Data?.TryGetValue("deepLink", out deepLink);

        ShowLocalNotification(title, body, deepLink);
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
