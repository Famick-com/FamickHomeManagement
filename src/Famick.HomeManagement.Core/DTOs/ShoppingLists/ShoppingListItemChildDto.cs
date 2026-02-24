namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Represents a child product option for a parent product on a shopping list.
/// Used when the shopping list item has a parent product with child variants.
/// </summary>
public class ShoppingListItemChildDto
{
    /// <summary>
    /// The child product ID
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Name of the child product
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Store's external product ID (for cart integration)
    /// </summary>
    public string? ExternalProductId { get; set; }

    /// <summary>
    /// Last known price from the store
    /// </summary>
    public decimal? LastKnownPrice { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// Aisle location in the store
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Shelf location in the store
    /// </summary>
    public string? Shelf { get; set; }

    /// <summary>
    /// Department in the store
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Whether the product is in stock at the store
    /// </summary>
    public bool? InStock { get; set; }

    /// <summary>
    /// Product image URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Whether this child has store metadata
    /// </summary>
    public bool HasStoreMetadata { get; set; }

    /// <summary>
    /// How much of this child has been purchased/checked off
    /// </summary>
    public decimal PurchasedQuantity { get; set; }
}
