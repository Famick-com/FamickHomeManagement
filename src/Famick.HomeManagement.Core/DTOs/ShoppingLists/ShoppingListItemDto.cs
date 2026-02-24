namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ShoppingListItemDto
{
    public Guid Id { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductName { get; set; }
    public decimal Amount { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? Note { get; set; }
    public bool IsPurchased { get; set; }
    public DateTime? PurchasedAt { get; set; }
    public decimal PurchasedQuantity { get; set; }
    public DateTime? BestBeforeDate { get; set; }

    // Product tracking fields for date prompting logic
    public bool TracksBestBeforeDate { get; set; }
    public int DefaultBestBeforeDays { get; set; }
    public Guid? DefaultLocationId { get; set; }

    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public string? Department { get; set; }
    public string? ExternalProductId { get; set; }
    public decimal? Price { get; set; }

    // Image URL (from linked product or store integration)
    public string? ImageUrl { get; set; }
    public string? Barcode { get; set; }

    /// <summary>
    /// All barcodes associated with the linked product (from ProductBarcodes).
    /// Used by mobile clients for offline barcode matching.
    /// </summary>
    public List<string> Barcodes { get; set; } = new();

    // Parent/child product support
    /// <summary>
    /// Whether this item's product is a parent product with child variants
    /// </summary>
    public bool IsParentProduct { get; set; }

    /// <summary>
    /// Whether this parent product has any child products
    /// </summary>
    public bool HasChildren { get; set; }

    /// <summary>
    /// Number of child products under this parent
    /// </summary>
    public int ChildProductCount { get; set; }

    /// <summary>
    /// Whether any child products have store metadata for the current store
    /// </summary>
    public bool HasChildrenAtStore { get; set; }

    /// <summary>
    /// Total quantity checked off across all child products
    /// </summary>
    public decimal ChildPurchasedQuantity { get; set; }

    /// <summary>
    /// Remaining quantity. For parent products: Amount - ChildPurchasedQuantity.
    /// For non-parent products: Amount - PurchasedQuantity. Can be negative if user bought extra.
    /// </summary>
    public decimal RemainingQuantity => IsParentProduct
        ? Amount - ChildPurchasedQuantity
        : Amount - PurchasedQuantity;

    /// <summary>
    /// Child products with store metadata (populated on demand)
    /// </summary>
    public List<ShoppingListItemChildDto>? ChildProducts { get; set; }

    /// <summary>
    /// Parsed child purchase entries (from ChildPurchasesJson)
    /// </summary>
    public List<ChildPurchaseEntry>? ChildPurchases { get; set; }
}
