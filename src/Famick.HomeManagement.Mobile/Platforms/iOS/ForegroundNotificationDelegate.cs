using Foundation;
using UserNotifications;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Handles notification presentation when the app is in the foreground
/// and notification tap actions for deep link navigation.
/// </summary>
public class ForegroundNotificationDelegate : UNUserNotificationCenterDelegate
{
    /// <summary>Category id (registered with a custom dismiss action) set on dismissible pushes.</summary>
    public const string DismissibleCategoryId = "FAMICK_DISMISSIBLE";

    // Runtime value of UNNotificationDismissActionIdentifier (stable Apple constant).
    private const string DismissActionIdentifier = "com.apple.UNNotificationDismissActionIdentifier";

    /// <summary>
    /// Show banner + sound even when the app is in the foreground.
    /// </summary>
    public override void WillPresentNotification(
        UNUserNotificationCenter center,
        UNNotification notification,
        Action<UNNotificationPresentationOptions> completionHandler)
    {
        completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound);
    }

    /// <summary>
    /// Handle notification tap (deep link) and notification dismissal (mark the in-app notification read).
    /// </summary>
    public override void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        var userInfo = response.Notification.Request.Content.UserInfo;

        // The user dismissed the notification (best-effort on iOS — explicit dismissals only).
        if (response.ActionIdentifier == DismissActionIdentifier)
        {
            if (userInfo.TryGetValue(new NSString("notificationId"), out var idObj)
                && idObj is NSString notificationId)
            {
                // Await before completing: the app may have been launched in the background just for
                // this and could suspend right after completionHandler.
                Task.Run(async () =>
                {
                    try { await Services.NotificationActionHelper.MarkReadAsync(notificationId.ToString()); }
                    finally { completionHandler(); }
                });
            }
            else
            {
                completionHandler();
            }
            return;
        }

        // Scheduled offline reminders carry a (possibly relative) Shell deep link that
        // new Uri(...) can't parse — route it through the reminder-safe navigator.
        if (userInfo.TryGetValue(new NSString(NotificationScheduler.DeepLinkKey), out var reminderLinkObj)
            && reminderLinkObj is NSString reminderLink
            && !string.IsNullOrEmpty(reminderLink.ToString()))
        {
            App.NavigateToReminderDeepLink(reminderLink.ToString());
        }
        else if (userInfo.TryGetValue(new NSString("deepLink"), out var deepLinkObj)
            && deepLinkObj is NSString deepLink
            && !string.IsNullOrEmpty(deepLink.ToString()))
        {
            var uri = new Uri(deepLink.ToString());
            App.HandleDeepLink(uri);
        }

        completionHandler();
    }
}
