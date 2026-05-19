using Famick.HomeManagement.Mobile.Services;
using Foundation;
using UIKit;
using UserNotifications;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS APNs push token provider. Requests notification permission and
/// registers for remote notifications to obtain the device token.
/// </summary>
public class PushTokenProvider : IPushTokenProvider
{
    private static TaskCompletionSource<string?>? _tokenTcs;

    public bool IsSupported => true;
    public int PlatformId => 1;
#pragma warning disable CS0067 // iOS doesn't have FCM-style token refresh; event satisfies interface
    public event EventHandler<string>? TokenRefreshed;
#pragma warning restore CS0067

    public async Task<string?> GetTokenAsync()
    {
        var center = UNUserNotificationCenter.Current;
        var (granted, error) = await center.RequestAuthorizationAsync(
            UNAuthorizationOptions.Alert | UNAuthorizationOptions.Badge | UNAuthorizationOptions.Sound);

        if (!granted)
        {
            Console.WriteLine($"[PushTokenProvider.iOS] Permission denied: {error?.LocalizedDescription}");
            return null;
        }

        // Capture the TCS locally so the timeout callback below can't
        // dereference a null field if the timer fires after we've already
        // cleared _tokenTcs at the end of this method.
        var tcs = new TaskCompletionSource<string?>();
        _tokenTcs = tcs;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            UIApplication.SharedApplication.RegisterForRemoteNotifications();
        });

        // Wait up to 10 seconds for the delegate callback
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        cts.Token.Register(() => tcs.TrySetResult(null));

        var token = await tcs.Task;
        _tokenTcs = null;
        return token;
    }

    /// <summary>
    /// Called from AppDelegate.RegisteredForRemoteNotifications.
    /// </summary>
    public static void HandleRegistration(NSData deviceToken)
    {
        var bytes = new byte[deviceToken.Length];
        System.Runtime.InteropServices.Marshal.Copy(deviceToken.Bytes, bytes, 0, (int)deviceToken.Length);
        var token = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        Console.WriteLine($"[PushTokenProvider.iOS] Got APNs token: {token[..8]}...");
        _tokenTcs?.TrySetResult(token);
    }

    /// <summary>
    /// Called from AppDelegate.FailedToRegisterForRemoteNotifications.
    /// </summary>
    public static void HandleRegistrationFailure(NSError error)
    {
        Console.WriteLine($"[PushTokenProvider.iOS] Registration failed: {error.LocalizedDescription}");
        _tokenTcs?.TrySetResult(null);
    }
}
