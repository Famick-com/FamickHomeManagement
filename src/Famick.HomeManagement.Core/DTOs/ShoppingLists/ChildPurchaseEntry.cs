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

    /// <summary>
    /// True while the child product does not exist yet: the entry was recorded by a scan of a
    /// master catalog child barcode, and moving the item to inventory creates the child product
    /// with <see cref="ChildProductId"/> as its id.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// The scanned barcode of a pending child; it is attached to the child product when it is created.
    /// </summary>
    public string? Barcode { get; set; }
}
