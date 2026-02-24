namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Request to send a specific child product to the store's online cart.
/// </summary>
public class SendChildToCartRequest
{
    /// <summary>
    /// The child product ID to send to cart
    /// </summary>
    public Guid ChildProductId { get; set; }

    /// <summary>
    /// Quantity to add to cart
    /// </summary>
    public int Quantity { get; set; } = 1;
}
