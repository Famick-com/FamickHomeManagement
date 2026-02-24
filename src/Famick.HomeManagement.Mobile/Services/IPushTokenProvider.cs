namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Platform-specific provider for push notification device tokens.
/// </summary>
public interface IPushTokenProvider
{
    /// <summary>
    /// Whether push notifications are supported on this platform.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Gets the current push notification token, requesting permission if needed.
    /// Returns null if permission denied or unavailable.
    /// </summary>
    Task<string?> GetTokenAsync();

    /// <summary>
    /// Platform identifier: 1 = iOS (APNs), 2 = Android (FCM).
    /// </summary>
    int PlatformId { get; }

    /// <summary>
    /// Fired when the platform issues a new token (e.g. FCM token refresh).
    /// </summary>
    event EventHandler<string>? TokenRefreshed;
}
