namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Request to add a product to a shopping list (from Stock Overview or barcode scan)
/// </summary>
public class AddToShoppingListRequest
{
    public Guid ShoppingListId { get; set; }
    public Guid? ProductId { get; set; }
    public string? Barcode { get; set; }

    /// <summary>
    /// Product name for quick-add when no existing product is found
    /// </summary>
    public string? ProductName { get; set; }

    public decimal Amount { get; set; } = 1;
    public string? Note { get; set; }
    public bool LookupInStore { get; set; } = true;

    /// <summary>
    /// If true, mark the item as purchased immediately (for shopping mode app)
    /// </summary>
    public bool IsPurchased { get; set; }

    /// <summary>
    /// Aisle location from store integration (provided by mobile app when already looked up)
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Department from store integration
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// External product ID from store integration (for cart push)
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// Price from store integration
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Image URL from store integration or product lookup
    /// </summary>
    public string? ImageUrl { get; set; }
}
