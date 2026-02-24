namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Tracks a purchase of a specific child product when checking off items.
/// Stored as JSON in ShoppingListItem.ChildPurchasesJson.
/// </summary>
public class ChildPurchaseEntry
{
    /// <summary>
    /// The child product ID that was purchased
    /// </summary>
    public Guid ChildProductId { get; set; }

    /// <summary>
    /// Name of the child product (for display without lookup)
    /// </summary>
    public string ChildProductName { get; set; } = string.Empty;

    /// <summary>
    /// Quantity purchased
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Store's external product ID (for reference)
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// When this entry was recorded
    /// </summary>
    public DateTime PurchasedAt { get; set; }
}
