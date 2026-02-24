namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

/// <summary>
/// Result from searching the store for products to add as children.
/// </summary>
public class StoreProductSearchResult
{
    /// <summary>
    /// Store's external product ID
    /// </summary>
    public string ExternalProductId { get; set; } = string.Empty;

    /// <summary>
    /// Product name from the store
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Product description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Price at the store
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Price unit (e.g., "each", "lb")
    /// </summary>
    public string? PriceUnit { get; set; }

    /// <summary>
    /// Aisle location
    /// </summary>
    public string? Aisle { get; set; }

    /// <summary>
    /// Department
    /// </summary>
    public string? Department { get; set; }

    /// <summary>
    /// Product image URL
    /// </summary>
    public string? ImageUrl { get; set; }

    /// <summary>
    /// Product barcode if available
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// Whether the product is in stock
    /// </summary>
    public bool? InStock { get; set; }

    /// <summary>
    /// Existing local product ID if this store product is already linked
    /// </summary>
    public Guid? LinkedProductId { get; set; }
}
