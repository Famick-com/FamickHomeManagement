namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Describes the capabilities supported by a store integration plugin.
/// </summary>
public class StoreIntegrationCapabilities
{
    /// <summary>
    /// Whether OAuth authentication is required to use this plugin.
    /// If false, the plugin works without authentication (e.g., public APIs).
    /// If true, users must complete OAuth flow before using the plugin.
    /// </summary>
    public bool RequiresOAuth { get; set; }

    /// <summary>
    /// Can search products by name/barcode
    /// </summary>
    public bool HasProductLookup { get; set; }

    /// <summary>
    /// Can search products at specific store locations
    /// </summary>
    public bool HasStoreProductLookup { get; set; }

    /// <summary>
    /// Can add items to user's shopping cart (single cart per user)
    /// </summary>
    public bool HasShoppingCart { get; set; }

    /// <summary>
    /// Can read user's current shopping cart
    /// </summary>
    public bool CanReadShoppingCart { get; set; }

    /// <summary>
    /// Can download product images for local storage
    /// </summary>
    public bool CanDownloadProductImages { get; set; }

    /// <summary>
    /// Creates a default capabilities instance with all features disabled
    /// </summary>
    public static StoreIntegrationCapabilities None => new();

    /// <summary>
    /// Creates a capabilities instance for plugins that only support product lookup (no OAuth required)
    /// </summary>
    public static StoreIntegrationCapabilities ProductLookupOnly => new()
    {
        RequiresOAuth = false,
        HasProductLookup = true,
        HasStoreProductLookup = true,
        CanDownloadProductImages = true
    };

    /// <summary>
    /// Creates a capabilities instance for plugins with full feature support (OAuth required)
    /// </summary>
    public static StoreIntegrationCapabilities Full => new()
    {
        RequiresOAuth = true,
        HasProductLookup = true,
        HasStoreProductLookup = true,
        HasShoppingCart = true,
        CanReadShoppingCart = true,
        CanDownloadProductImages = true
    };
}
