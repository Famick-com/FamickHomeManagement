namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ScanPurchaseResultDto
{
    /// <summary>
    /// Updated item state after the scan
    /// </summary>
    public ShoppingListItemDto Item { get; set; } = null!;

    /// <summary>
    /// Whether this scan caused the item to be fully completed
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Remaining quantity after this scan (can be negative if over-scanned)
    /// </summary>
    public decimal RemainingQuantity { get; set; }
}
