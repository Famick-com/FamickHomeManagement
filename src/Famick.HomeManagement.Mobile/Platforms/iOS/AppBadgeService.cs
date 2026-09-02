using Famick.HomeManagement.Mobile.Services;
using UIKit;
using UserNotifications;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS app-icon badge setter. Sets the springboard badge to the given absolute value on the main
/// thread, using the modern <c>SetBadgeCount</c> API on iOS 16+ and the legacy property below it.
/// </summary>
public class AppBadgeService : IAppBadgeService
{
    public void SetBadge(int count)
    {
        var value = Math.Max(0, count);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (OperatingSystem.IsIOSVersionAtLeast(16))
                {
                    UNUserNotificationCenter.Current.SetBadgeCount(value, null);
                }
                else
                {
                    // Deprecated on iOS 17+, but this branch only runs on iOS 15 (min target).
#pragma warning disable CA1422
                    UIApplication.SharedApplication.ApplicationIconBadgeNumber = value;
#pragma warning restore CA1422
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppBadgeService] Failed to set badge: {ex.Message}");
            }
        });
    }
}
