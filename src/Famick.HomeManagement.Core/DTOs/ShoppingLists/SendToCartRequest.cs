namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Request to send shopping list items to the store's cart
/// </summary>
public class SendToCartRequest
{
    /// <summary>
    /// Specific item IDs to send. If empty, all unpurchased items with ExternalProductId will be sent.
    /// </summary>
    public List<Guid> ItemIds { get; set; } = new();
}
