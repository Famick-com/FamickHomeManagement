namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Result from an OAuth token exchange or refresh operation
/// </summary>
public class OAuthTokenResult
{
    /// <summary>
    /// Whether the token operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The access token for API calls
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// The refresh token for obtaining new access tokens
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// When the access token expires
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Error message if the operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Creates a successful token result
    /// </summary>
    public static OAuthTokenResult Ok(string accessToken, string? refreshToken, DateTime expiresAt) => new()
    {
        Success = true,
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        ExpiresAt = expiresAt
    };

    /// <summary>
    /// Creates a failed token result
    /// </summary>
    public static OAuthTokenResult Fail(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Result from a store location search
/// </summary>
public class StoreLocationResult
{
    /// <summary>
    /// External store location ID from the integration provider
    /// </summary>
    public string ExternalLocationId { get; set; } = string.Empty;

    /// <summary>
    /// Store name (e.g., "Kroger #12345")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Chain/brand identifier (e.g., "kroger", "ralphs", "fred-meyer")
    /// </summary>
    public string? ChainId { get; set; }

    /// <summary>
    /// Chain/brand display name (e.g., "Kroger", "Ralphs", "Fred Meyer")
    /// </summary>
    public string? ChainName { get; set; }

    /// <summary>
    /// Full street address
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// City
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// State/province code
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// ZIP/postal code
    /// </summary>
    public string? ZipCode { get; set; }

    /// <summary>
    /// Store phone number
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Store latitude
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Store longitude
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Distance from search location in miles
    /// </summary>
    public double? DistanceMiles { get; set; }

    /// <summary>
    /// Formats the full address as a single line
    /// </summary>
    public string? FullAddress => !string.IsNullOrEmpty(Address)
        ? $"{Address}, {City}, {State} {ZipCode}"
        : null;
}

/// <summary>
/// Result from a store product search or lookup
/// </summary>
public class StoreProductResult
{
    /// <summary>
    /// Store's internal product ID/SKU
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Brand name
    /// </summary>
    public string? Brand { get; set; }

    /// <summary>
    /// Product barcode (UPC/EAN)
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// URL to product image
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Current price
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb", "oz")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// Sale/promotional price (if on sale)
    /// </summary>
    public decimal? SalePrice { get; set; }

    /// <summary>
    /// Aisle location in the store
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Shelf location in the store
    /// </summary>
    public string? Shelf { get; set; }

    /// <summary>
    /// Department or category
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Whether the product is currently in stock
    /// </summary>
    public bool? InStock { get; set; }

    /// <summary>
    /// Product size/weight description
    /// </summary>
    public string? Size { get; set; }

    /// <summary>
    /// URL to the product page on the store's website
    /// </summary>
    public string? ProductUrl { get; set; }

    /// <summary>
    /// Categories of the product
    /// </summary>
    public List<string> Categories {get;set;} = new();

    /// <summary>
    /// Description of the product
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// How long this data should be cached, derived from HTTP Cache-Control headers.
    /// Null means no cache duration was provided by the API.
    /// </summary>
    public TimeSpan? CacheDuration { get; set; }
}

/// <summary>
/// Result from getting a user's shopping cart (single cart per user)
/// </summary>
public class ShoppingCartResult
{
    /// <summary>
    /// Items currently in the cart
    /// </summary>
    public List<CartItemResult> Items { get; set; } = new();

    /// <summary>
    /// Subtotal of all items in the cart
    /// </summary>
    public decimal? Subtotal { get; set; }

    /// <summary>
    /// The store location ID the cart is associated with
    /// </summary>
    public string? StoreLocationId { get; set; }
}

/// <summary>
/// Represents an item in the shopping cart
/// </summary>
public class CartItemResult
{
    /// <summary>
    /// Store's internal product ID/SKU
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Quantity in cart
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Price per item
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// URL to product image
    /// </summary>
    public string? ImageUrl { get; set; }
}

/// <summary>
/// Request to add/update items in a shopping cart
/// </summary>
public class CartItemRequest
{
    /// <summary>
    /// Store's internal product ID/SKU
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Quantity to add/set
    /// </summary>
    public int Quantity { get; set; }
}

/// <summary>
/// Exception thrown when a store API call fails due to authentication issues (e.g., expired token, 401 response).
/// This allows the service layer to catch this specific exception and attempt token refresh with retry.
/// </summary>
public class StoreAuthenticationException : Exception
{
    /// <summary>
    /// The plugin ID that encountered the authentication failure
    /// </summary>
    public string PluginId { get; }

    /// <summary>
    /// The HTTP status code returned by the API, if applicable
    /// </summary>
    public int? HttpStatusCode { get; }

    public StoreAuthenticationException(string pluginId, string message, int? httpStatusCode = null)
        : base(message)
    {
        PluginId = pluginId;
        HttpStatusCode = httpStatusCode;
    }

    public StoreAuthenticationException(string pluginId, string message, Exception innerException, int? httpStatusCode = null)
        : base(message, innerException)
    {
        PluginId = pluginId;
        HttpStatusCode = httpStatusCode;
    }
}
