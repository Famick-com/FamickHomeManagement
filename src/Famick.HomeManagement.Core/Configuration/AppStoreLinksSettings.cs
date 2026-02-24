namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Configuration settings for mobile app store links and deep linking
/// </summary>
public class AppStoreLinksSettings
{
    /// <summary>
    /// URL to the app on the Apple App Store
    /// </summary>
    public string AppleAppStore { get; set; } = string.Empty;

    /// <summary>
    /// URL to the app on the Google Play Store
    /// </summary>
    public string GooglePlayStore { get; set; } = string.Empty;

    /// <summary>
    /// Custom URL scheme for deep linking (e.g., "famickshopping")
    /// </summary>
    public string DeepLinkScheme { get; set; } = "famickshopping";
}
