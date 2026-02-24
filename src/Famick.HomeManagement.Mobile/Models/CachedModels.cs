using System.Text.Json;
using SQLite;

namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Cached shopping session for offline use.
/// </summary>
public class ShoppingSession
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public Guid ShoppingListId { get; set; }
    public string ShoppingListName { get; set; } = "";
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public bool IsActive { get; set; }

    [Ignore]
    public List<CachedShoppingListItem> Items { get; set; } = new();
}

/// <summary>
/// Cached shopping list item for offline use.
/// </summary>
public class CachedShoppingListItem
{
    [PrimaryKey]
    public Guid Id { get; set; }

    [Indexed]
    public Guid SessionId { get; set; }

    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string? LocalImagePath { get; set; }
    public string? ImageUrl { get; set; }
    public decimal Amount { get; set; }
    public string? QuantityUnitName { get; set; }
    public string? Note { get; set; }
    public bool IsPurchased { get; set; }
    public bool OriginalIsPurchased { get; set; }
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
    public string? Barcode { get; set; }

    /// <summary>
    /// JSON-serialized list of all barcodes for the linked product.
    /// SQLite cannot store lists directly.
    /// </summary>
    public string? BarcodesJson { get; set; }

    public int SortOrder { get; set; }
    public bool IsNewItem { get; set; }

    // Parent/child product support
    /// <summary>
    /// Whether this item's product is a parent product with child variants
    /// </summary>
    public bool IsParentProduct { get; set; }

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
    /// JSON tracking of child purchases (stored for offline sync)
    /// </summary>
    public string? ChildPurchasesJson { get; set; }

    /// <summary>
    /// All barcodes for the linked product, deserialized from BarcodesJson.
    /// </summary>
    [Ignore]
    public List<string> Barcodes
    {
        get
        {
            if (string.IsNullOrEmpty(BarcodesJson)) return new();
            try { return JsonSerializer.Deserialize<List<string>>(BarcodesJson) ?? new(); }
            catch { return new(); }
        }
    }

    [Ignore]
    public bool HasPrice => Price.HasValue;

    [Ignore]
    public bool HasImage => !string.IsNullOrEmpty(LocalImagePath);

    [Ignore]
    public ImageSource? ImageSource => HasImage ? ImageSource.FromFile(LocalImagePath) : null;

    /// <summary>
    /// Remaining quantity to purchase.
    /// Parent products: Amount - ChildPurchasedQuantity.
    /// Non-parent products: Amount - PurchasedQuantity.
    /// </summary>
    [Ignore]
    public decimal RemainingQuantity => IsParentProduct
        ? Amount - ChildPurchasedQuantity
        : Amount - PurchasedQuantity;

    /// <summary>
    /// Whether this is a parent product that needs child selection
    /// </summary>
    [Ignore]
    public bool NeedsChildSelection => IsParentProduct && HasChildrenAtStore && !IsPurchased;
}

/// <summary>
/// Queued offline operation for sync.
/// </summary>
public class OfflineOperation
{
    [PrimaryKey]
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }
    public string OperationType { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public bool IsCompleted { get; set; }
    public int RetryCount { get; set; }
}

