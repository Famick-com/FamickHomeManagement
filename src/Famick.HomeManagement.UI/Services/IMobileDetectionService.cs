namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Service for detecting mobile devices and handling deep link attempts
/// </summary>
public interface IMobileDetectionService
{
    /// <summary>
    /// Checks if the current device is a mobile device (phone or tablet)
    /// </summary>
    /// <returns>True if mobile device, false otherwise</returns>
    Task<bool> IsMobileDeviceAsync();

    /// <summary>
    /// Gets the mobile platform (iOS or Android)
    /// </summary>
    /// <returns>"iOS", "Android", or null if not a mobile device</returns>
    Task<string?> GetMobilePlatformAsync();

    /// <summary>
    /// Attempts to open the app via deep link, with a fallback timeout
    /// </summary>
    /// <param name="deepLinkUrl">The deep link URL to attempt</param>
    /// <param name="timeoutMs">Timeout in milliseconds before considering the link failed</param>
    /// <returns>True if the deep link was attempted (doesn't guarantee app opened)</returns>
    Task<bool> TryOpenAppAsync(string deepLinkUrl, int timeoutMs = 2000);

    /// <summary>
    /// Checks if the app download banner should be shown (not already dismissed in this session)
    /// </summary>
    /// <returns>True if banner should be shown</returns>
    Task<bool> ShouldShowAppBannerAsync();

    /// <summary>
    /// Dismisses the app download banner for the current session
    /// </summary>
    Task DismissAppBannerAsync();
}
