using Foundation;
using UIKit;
using Intents;
using Microsoft.Maui.Platform;
using UserNotifications;
using Famick.HomeManagement.Mobile.Platforms.iOS;

namespace Famick.HomeManagement.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
    {
        var result = base.FinishedLaunching(application, launchOptions);

        // Set notification delegate for foreground presentation and tap handling
        UNUserNotificationCenter.Current.Delegate = new ForegroundNotificationDelegate();

        // Donate Siri Shortcut for quick consume
        DonateQuickConsumeShortcut();

        return result;
    }

    [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
    public void RegisteredForRemoteNotifications(UIApplication application, NSData deviceToken)
    {
        PushTokenProvider.HandleRegistration(deviceToken);
    }

    [Export("application:didFailToRegisterForRemoteNotificationsWithError:")]
    public void FailedToRegisterForRemoteNotifications(UIApplication application, NSError error)
    {
        PushTokenProvider.HandleRegistrationFailure(error);
    }

    public override void OnActivated(UIApplication application)
    {
        base.OnActivated(application);

        // Ensure the main window background is white to match the app theme and cover the safe area
        if (UIDevice.CurrentDevice.CheckSystemVersion(13, 0)) // Available iOS 13+
        {
            foreach (var scene in application.ConnectedScenes)
            {
                if (scene is UIWindowScene windowScene)
                {
                    var window = windowScene.Windows.FirstOrDefault();
                    if (window != null)
                    {
                        // Use SystemBackground for proper light/dark mode support
                        window.BackgroundColor = UIColor.SystemBackground;
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handle URL scheme deep links (iOS 9+)
    /// </summary>
    public override bool OpenUrl(UIApplication application, NSUrl url, NSDictionary options)
    {
        if (url != null && (url.Scheme == "famickshopping" || url.Scheme == "famick"))
        {
            var uri = new Uri(url.AbsoluteString ?? string.Empty);
            App.HandleDeepLink(uri);
            return true;
        }

        return base.OpenUrl(application, url, options);
    }

    /// <summary>
    /// Handle continuing user activity (for Siri Shortcuts)
    /// </summary>
    public override bool ContinueUserActivity(UIApplication application, NSUserActivity userActivity, UIApplicationRestorationHandler completionHandler)
    {
        if (userActivity.ActivityType == "QuickConsumeActivity" ||
            userActivity.ActivityType == NSUserActivityType.BrowsingWeb.ToString())
        {
            // Check if this is our quick consume shortcut
            if (userActivity.UserInfo?.ContainsKey(new NSString("action")) == true)
            {
                var action = userActivity.UserInfo["action"]?.ToString();
                if (action == "quick-consume")
                {
                    App.PendingQuickConsume = true;
                    return true;
                }
            }
        }

        return base.ContinueUserActivity(application, userActivity, completionHandler);
    }

    /// <summary>
    /// Donates a Siri Shortcut for quick consume action
    /// </summary>
    private void DonateQuickConsumeShortcut()
    {
        try
        {
            // Create user activity for Siri Shortcuts (iOS 12+)
            if (!UIDevice.CurrentDevice.CheckSystemVersion(12, 0))
                return;

            var activity = new NSUserActivity("QuickConsumeActivity")
            {
                Title = "Consume from Pantry",
                EligibleForSearch = true,
                EligibleForPrediction = true,
            };

            // Set suggested invocation phrase
            activity.SuggestedInvocationPhrase = "Consume from pantry";

            // Add custom user info
            activity.UserInfo = NSDictionary.FromObjectAndKey(
                new NSString("quick-consume"),
                new NSString("action"));

            // Make it current to donate to Siri
            activity.BecomeCurrent();

            Console.WriteLine("[AppDelegate] Donated Quick Consume Siri Shortcut");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppDelegate] Error donating Siri Shortcut: {ex.Message}");
        }
    }
}
