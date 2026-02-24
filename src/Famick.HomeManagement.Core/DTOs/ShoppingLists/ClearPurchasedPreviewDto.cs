namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Preview data for the clear purchased dialog
/// </summary>
public class ClearPurchasedPreviewDto
{
    /// <summary>
    /// Items that can be moved to inventory (have ProductId)
    /// </summary>
    public List<ClearPurchasedItemDto> ItemsWithProducts { get; set; } = new();

    /// <summary>
    /// Items without products that will become TODO tasks
    /// </summary>
    public List<ClearPurchasedItemDto> ItemsWithoutProducts { get; set; } = new();

    /// <summary>
    /// Count of items that can be moved to inventory
    /// </summary>
    public int InventoryItemCount => ItemsWithProducts.Count;

    /// <summary>
    /// Count of items that will become TODO tasks
    /// </summary>
    public int TodoItemCount => ItemsWithoutProducts.Count;
}

/// <summary>
/// Individual purchased item for clear preview
/// </summary>
public class ClearPurchasedItemDto
{
    /// <summary>
    /// Shopping list item ID
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Product ID (null for items without linked products)
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Product or item name
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Amount purchased
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Quantity unit name (if product exists)
    /// </summary>
    public string? QuantityUnitName { get; set; }

    /// <summary>
    /// Whether the product tracks best before dates
    /// </summary>
    public bool TracksBestBeforeDate { get; set; }

    /// <summary>
    /// Default days to add for best before date calculation
    /// </summary>
    public int DefaultBestBeforeDays { get; set; }

    /// <summary>
    /// Product's default storage location ID
    /// </summary>
    public Guid? DefaultLocationId { get; set; }

    /// <summary>
    /// Product's default storage location name
    /// </summary>
    public string? DefaultLocationName { get; set; }

    /// <summary>
    /// Best before date captured when item was marked purchased
    /// </summary>
    public DateTime? BestBeforeDate { get; set; }

    /// <summary>
    /// Product image URL from store integration
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Product barcode from store integration
    /// </summary>
    public string? Barcode { get; set; }

    // UI binding properties (set by client, not server)

    /// <summary>
    /// Selected location ID for inventory (UI binding)
    /// </summary>
    public Guid? SelectedLocationId { get; set; }

    /// <summary>
    /// Selected best before date for inventory (UI binding)
    /// </summary>
    public DateTime? SelectedBestBeforeDate { get; set; }
}
