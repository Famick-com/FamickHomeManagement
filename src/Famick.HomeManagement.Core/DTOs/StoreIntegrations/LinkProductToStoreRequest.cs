namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// Request to link a product to a store product
/// </summary>
public class LinkProductToStoreRequest
{
    /// <summary>
    /// Store's internal product ID
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Current price
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// Aisle location
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Shelf location
    /// </summary>
    public string? Shelf { get; set; }

    /// <summary>
    /// Department
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Whether the product is in stock
    /// </summary>
    public bool? InStock { get; set; }

    /// <summary>
    /// URL to the product page on the store's website
    /// </summary>
    public string? ProductUrl { get; set; }

    /// <summary>
    /// How long the cached data should be valid. Set by the service when data comes from an API source.
    /// Null means no expiration (user-entered data).
    /// </summary>
    public TimeSpan? CacheDuration { get; set; }
}
