using Android;
using Android.Content.PM;
using Android.Gms.Extensions;
using Android.OS;
using Famick.HomeManagement.Mobile.Services;
using Firebase.Messaging;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android FCM push token provider. Requests POST_NOTIFICATIONS permission (Android 13+)
/// and retrieves the FCM registration token.
/// </summary>
public class PushTokenProvider : IPushTokenProvider
{
    private static EventHandler<string>? _staticTokenRefreshed;

    public bool IsSupported => true;
    public int PlatformId => 2;

    public event EventHandler<string>? TokenRefreshed
    {
        add => _staticTokenRefreshed += value;
        remove => _staticTokenRefreshed -= value;
    }

    public async Task<string?> GetTokenAsync()
    {
        // Request POST_NOTIFICATIONS permission on Android 13+ (API 33)
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            var status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                Console.WriteLine("[PushTokenProvider.Android] POST_NOTIFICATIONS permission denied");
                return null;
            }
        }

        try
        {
            // GetToken() returns an Android Task; use AsAsync to bridge to .NET Task
            var tokenResult = await FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.Object>();
            var token = tokenResult?.ToString();

            if (!string.IsNullOrEmpty(token))
            {
                Console.WriteLine($"[PushTokenProvider.Android] Got FCM token: {token[..8]}...");
                return token;
            }

            Console.WriteLine("[PushTokenProvider.Android] FCM returned null token");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PushTokenProvider.Android] Error getting FCM token: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Called from FamickFirebaseMessagingService.OnNewToken when FCM refreshes the token.
    /// </summary>
    public static void HandleTokenRefresh(string token)
    {
        Console.WriteLine($"[PushTokenProvider.Android] Token refreshed: {token[..8]}...");
        _staticTokenRefreshed?.Invoke(null, token);
    }
}
