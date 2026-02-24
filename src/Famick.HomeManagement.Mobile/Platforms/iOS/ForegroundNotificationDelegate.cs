using Foundation;
using UserNotifications;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// Handles notification presentation when the app is in the foreground
/// and notification tap actions for deep link navigation.
/// </summary>
public class ForegroundNotificationDelegate : UNUserNotificationCenterDelegate
{
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
    /// Handle notification tap — navigate via deep link if present.
    /// </summary>
    public override void DidReceiveNotificationResponse(
        UNUserNotificationCenter center,
        UNNotificationResponse response,
        Action completionHandler)
    {
        var userInfo = response.Notification.Request.Content.UserInfo;
        if (userInfo.TryGetValue(new NSString("deepLink"), out var deepLinkObj)
            && deepLinkObj is NSString deepLink
            && !string.IsNullOrEmpty(deepLink.ToString()))
        {
            var uri = new Uri(deepLink.ToString());
            App.HandleDeepLink(uri);
        }

        completionHandler();
    }
}
