namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Request to move purchased shopping list items to inventory
/// </summary>
public class MoveToInventoryRequest
{
    /// <summary>
    /// The shopping list ID
    /// </summary>
    public Guid ShoppingListId { get; set; }

    /// <summary>
    /// List of items to move to inventory
    /// </summary>
    public List<MoveToInventoryItem> Items { get; set; } = new();
}

/// <summary>
/// Individual item to move to inventory
/// </summary>
public class MoveToInventoryItem
{
    /// <summary>
    /// The shopping list item ID
    /// </summary>
    public Guid ShoppingListItemId { get; set; }

    /// <summary>
    /// Product ID (null for items needing product setup)
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Product name for display/TODO creation
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// Amount purchased
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Price paid per unit
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Barcode if scanned
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Product image URL from store integration
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Best before / expiration date for inventory tracking
    /// </summary>
    public DateTime? BestBeforeDate { get; set; }

    /// <summary>
    /// Storage location ID for this item in inventory
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// External product ID from store integration (for linking product to store)
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// Shopping location ID (store) for linking product to store metadata
    /// </summary>
    public Guid? ShoppingLocationId { get; set; }

    /// <summary>
    /// Aisle location in store
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Shelf location in store
    /// </summary>
    public string? Shelf { get; set; }

    /// <summary>
    /// Department in store
    /// </summary>
    public string? Department { get; set; }
}
