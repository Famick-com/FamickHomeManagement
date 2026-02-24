using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Links a product to a store integration with store-specific metadata.
/// Stores external product ID, pricing, and location information from the store's system.
/// </summary>
public class ProductStoreMetadata : BaseEntity, ITenantEntity
{
    /// <summary>
    /// Tenant identifier for multi-tenancy isolation
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The product this metadata is for
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// The shopping location (store) this metadata is from
    /// </summary>
    public Guid ShoppingLocationId { get; set; }

    /// <summary>
    /// The store's internal product ID/SKU
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// Last known price from the store
    /// </summary>
    public decimal? LastKnownPrice { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb", "oz")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// When the price was last updated
    /// </summary>
    public DateTime? PriceUpdatedAt { get; set; }

    /// <summary>
    /// Aisle location in the store
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Shelf location in the store
    /// </summary>
    public string? Shelf { get; set; }

    /// <summary>
    /// Department or category in the store
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Whether the product is currently in stock
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
    /// When the cached API data expires. Null for user-entered data (no expiration).
    /// Set for API-sourced data to comply with store API Terms of Service.
    /// </summary>
    public DateTime? CacheExpiresAt { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
    public ShoppingLocation ShoppingLocation { get; set; } = null!;
}
