namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a product/item in the inventory system
/// </summary>
public class Product : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid LocationId { get; set; }
    public Guid QuantityUnitIdPurchase { get; set; }
    public Guid QuantityUnitIdStock { get; set; }
    public decimal QuantityUnitFactorPurchaseToStock { get; set; } = 1.0m;
    public decimal MinStockAmount { get; set; } = 0;
    public int DefaultBestBeforeDays { get; set; } = 0;
    public bool TracksBestBeforeDate { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Per-product override for expiry warning threshold (days before BestBeforeDate)
    public int? ExpiryWarningDays { get; set; }

    // Serving/package information (for weight calculations)
    public decimal? ServingSize { get; set; }
    public string? ServingUnit { get; set; }
    public decimal? ServingsPerContainer { get; set; }

    // Phase 2 - Product categorization and shopping
    public Guid? ProductGroupId { get; set; }
    public Guid? ShoppingLocationId { get; set; }

    // Parent-child hierarchy (for product variants/grouping)
    public Guid? ParentProductId { get; set; }

    // Navigation properties
    public Location Location { get; set; } = null!;
    public QuantityUnit QuantityUnitPurchase { get; set; } = null!;
    public QuantityUnit QuantityUnitStock { get; set; } = null!;
    public ICollection<ProductBarcode> Barcodes { get; set; } = new List<ProductBarcode>();
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

    // Phase 2 navigation properties
    public ProductGroup? ProductGroup { get; set; }
    public ShoppingLocation? ShoppingLocation { get; set; }

    // Parent-child hierarchy navigation
    public Product? ParentProduct { get; set; }
    public ICollection<Product> ChildProducts { get; set; } = new List<Product>();

    // Store-specific metadata (price, aisle, availability per store)
    public ICollection<ProductStoreMetadata> StoreMetadata { get; set; } = new List<ProductStoreMetadata>();

    // Nutrition data (optional, populated from external sources)
    public ProductNutrition? Nutrition { get; set; }

    /// <summary>
    /// Markdown-formatted attribution text for external data sources
    /// that contributed to this product's data (nutrition, images, etc.).
    /// Generated at lookup-apply time from plugin-provided attribution strings.
    /// </summary>
    public string? DataSourceAttribution { get; set; }
}
