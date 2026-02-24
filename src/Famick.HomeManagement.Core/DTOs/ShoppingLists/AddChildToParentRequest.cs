namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Request to add a child product to a parent item on the shopping list.
/// Can use an existing product or create an ad-hoc entry from store search.
/// </summary>
public class AddChildToParentRequest
{
    /// <summary>
    /// Existing product ID to add as child (if linking an existing product)
    /// </summary>
    public Guid? ProductId { get; set; }

    /// <summary>
    /// Product name (used for ad-hoc child when ProductId is null)
    /// </summary>
    public string? ProductName { get; set; }

    /// <summary>
    /// Store's external product ID (from store search, for cart integration)
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// Initial quantity to check off
    /// </summary>
    public decimal Quantity { get; set; } = 1;

    /// <summary>
    /// Optional best before date for inventory tracking
    /// </summary>
    public DateTime? BestBeforeDate { get; set; }
}
