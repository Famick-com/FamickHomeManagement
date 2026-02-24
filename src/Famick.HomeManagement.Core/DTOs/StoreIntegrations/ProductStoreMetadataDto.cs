namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// DTO for product-store metadata (price, location, availability)
/// </summary>
public class ProductStoreMetadataDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid ShoppingLocationId { get; set; }

    /// <summary>
    /// Product name (for display)
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// Shopping location name (for display)
    /// </summary>
    public string? ShoppingLocationName { get; set; }

    /// <summary>
    /// Store's internal product ID
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// Last known price
    /// </summary>
    public decimal? LastKnownPrice { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// When the price was last updated
    /// </summary>
    public DateTime? PriceUpdatedAt { get; set; }

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
    /// When availability was last checked
    /// </summary>
    public DateTime? AvailabilityCheckedAt { get; set; }

    /// <summary>
    /// URL to the product page on the store's website
    /// </summary>
    public string? ProductUrl { get; set; }

    /// <summary>
    /// Whether the cached API data has expired and should be refreshed
    /// </summary>
    public bool IsExpired { get; set; }

    /// <summary>
    /// When the cached API data expires (null for user-entered data)
    /// </summary>
    public DateTime? CacheExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
