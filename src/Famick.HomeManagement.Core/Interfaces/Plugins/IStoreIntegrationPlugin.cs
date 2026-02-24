namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Interface for store integration plugins that connect to external store APIs
/// (Kroger, Walmart, etc.) for OAuth authentication, store location lookup,
/// and product price/availability information.
/// </summary>
public interface IStoreIntegrationPlugin : IPlugin
{
    /// <summary>
    /// Plugin capabilities - indicates which features are supported.
    /// All operations are assumed to require OAuth authentication.
    /// </summary>
    StoreIntegrationCapabilities Capabilities { get; }

    #region OAuth Methods

    /// <summary>
    /// Get the OAuth authorization URL to redirect the user to for authentication.
    /// </summary>
    /// <param name="redirectUri">The URI to redirect back to after authentication</param>
    /// <param name="state">State parameter for CSRF protection (should include shopping location ID)</param>
    /// <returns>The full authorization URL to redirect the user to</returns>
    string GetAuthorizationUrl(string redirectUri, string state);

    /// <summary>
    /// Exchange an authorization code for access and refresh tokens.
    /// Called after the user completes OAuth authentication.
    /// </summary>
    /// <param name="code">The authorization code from the OAuth callback</param>
    /// <param name="redirectUri">The redirect URI (must match what was used in GetAuthorizationUrl)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Token result with access token, refresh token, and expiry</returns>
    Task<OAuthTokenResult> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken ct = default);

    /// <summary>
    /// Refresh an expired access token using the refresh token.
    /// </summary>
    /// <param name="refreshToken">The refresh token</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Token result with new access token, possibly new refresh token, and expiry</returns>
    Task<OAuthTokenResult> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);

    #endregion

    #region Store Location Methods

    /// <summary>
    /// Search for store locations by ZIP code.
    /// Does not require user authentication (uses client credentials).
    /// </summary>
    /// <param name="zipCode">ZIP/postal code to search near</param>
    /// <param name="radiusMiles">Search radius in miles (default 10)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of nearby store locations</returns>
    Task<List<StoreLocationResult>> SearchStoresByZipAsync(string zipCode, int radiusMiles = 10, CancellationToken ct = default);

    /// <summary>
    /// Search for store locations by GPS coordinates.
    /// Does not require user authentication (uses client credentials).
    /// </summary>
    /// <param name="latitude">Latitude</param>
    /// <param name="longitude">Longitude</param>
    /// <param name="radiusMiles">Search radius in miles (default 10)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of nearby store locations</returns>
    Task<List<StoreLocationResult>> SearchStoresByCoordinatesAsync(double latitude, double longitude, int radiusMiles = 10, CancellationToken ct = default);

    #endregion

    #region Product Methods

    /// <summary>
    /// Search for products at a specific store location.
    /// Requires user authentication (access token).
    /// </summary>
    /// <param name="accessToken">User's OAuth access token</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="query">Search query (product name or barcode)</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of matching products with store-specific data</returns>
    Task<List<StoreProductResult>> SearchProductsAsync(
        string accessToken,
        string storeLocationId,
        string query,
        int maxResults = 20,
        CancellationToken ct = default);

    /// <summary>
    /// Get product details by the store's product ID.
    /// Requires user authentication (access token).
    /// </summary>
    /// <param name="accessToken">User's OAuth access token</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="productId">Store's product ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Product details or null if not found</returns>
    Task<StoreProductResult?> GetProductAsync(
        string accessToken,
        string storeLocationId,
        string productId,
        CancellationToken ct = default);

    /// <summary>
    /// Lookup product by barcode at a specific store location.
    /// May not require authentication depending on the store API.
    /// </summary>
    /// <param name="accessToken">User's OAuth access token (may be optional for some stores)</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="barcode">Product barcode (UPC/EAN)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Product details or null if not found</returns>
    Task<StoreProductResult?> LookupProductByBarcodeAsync(
        string? accessToken,
        string storeLocationId,
        string barcode,
        CancellationToken ct = default);

    #endregion

    #region Shopping Cart Methods

    /// <summary>
    /// Get the user's current shopping cart (single cart per user).
    /// Requires user authentication (access token).
    /// Check Capabilities.CanReadShoppingCart before calling.
    /// </summary>
    /// <param name="accessToken">User's OAuth access token</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Shopping cart result or null if not supported</returns>
    Task<ShoppingCartResult?> GetShoppingCartAsync(
        string accessToken,
        string storeLocationId,
        CancellationToken ct = default);

    /// <summary>
    /// Add items to the user's shopping cart.
    /// Requires user authentication (access token).
    /// Check Capabilities.HasShoppingCart before calling.
    /// </summary>
    /// <param name="accessToken">User's OAuth access token</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="items">Items to add to the cart</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated shopping cart result or null if not supported</returns>
    Task<ShoppingCartResult?> AddToCartAsync(
        string accessToken,
        string storeLocationId,
        List<CartItemRequest> items,
        CancellationToken ct = default);

    /// <summary>
    /// Update the quantity of an item in the shopping cart.
    /// Requires user authentication (access token).
    /// Check Capabilities.HasShoppingCart before calling.
    /// </summary>
    /// <param name="accessToken">User's OAuth access token</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="productId">Store's product ID to update</param>
    /// <param name="quantity">New quantity (0 to remove)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated shopping cart result or null if not supported</returns>
    Task<ShoppingCartResult?> UpdateCartItemAsync(
        string accessToken,
        string storeLocationId,
        string productId,
        int quantity,
        CancellationToken ct = default);

    /// <summary>
    /// Remove an item from the shopping cart.
    /// Requires user authentication (access token).
    /// Check Capabilities.HasShoppingCart before calling.
    /// </summary>
    /// <param name="accessToken">User's OAuth access token</param>
    /// <param name="storeLocationId">External store location ID</param>
    /// <param name="productId">Store's product ID to remove</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Updated shopping cart result or null if not supported</returns>
    Task<ShoppingCartResult?> RemoveFromCartAsync(
        string accessToken,
        string storeLocationId,
        string productId,
        CancellationToken ct = default);

    #endregion
}
