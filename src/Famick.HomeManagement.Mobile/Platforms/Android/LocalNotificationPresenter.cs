using Android.App;
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Shared helper that posts a local notification whose tap re-opens the app carrying an offline
/// reminder deep link. Used by <see cref="ReminderAlarmReceiver"/> when an AlarmManager alarm fires.
/// </summary>
internal static class LocalNotificationPresenter
{
    public static void Show(Context context, int notificationId, string channelId, string title, string body, string? deepLink)
    {
        var launch = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
        if (launch != null)
        {
            launch.AddFlags(ActivityFlags.ClearTop | ActivityFlags.SingleTop);
            if (!string.IsNullOrEmpty(deepLink))
                launch.PutExtra(NotificationScheduler.ExtraDeepLink, deepLink);
        }

        var contentIntent = PendingIntent.GetActivity(
            context, notificationId, launch,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(context, channelId)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetStyle(new NotificationCompat.BigTextStyle().BigText(body))
            .SetAutoCancel(true)
            .SetPriority(NotificationCompat.PriorityDefault)
            .SetContentIntent(contentIntent);

        NotificationManagerCompat.From(context).Notify(notificationId, builder.Build());
    }
}
