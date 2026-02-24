namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class AddShoppingListItemRequest
{
    public Guid? ProductId { get; set; }
    public decimal Amount { get; set; } = 1;
    public string? Note { get; set; }

    // Optional: Store integration metadata (from product lookup)
    public string? ExternalProductId { get; set; }
    public string? Aisle { get; set; }
    public string? Shelf { get; set; }
    public string? Department { get; set; }
    public decimal? Price { get; set; }

    // Optional: Barcode for lookup
    public string? Barcode { get; set; }

    // Optional: Product name (for items not in local DB)
    public string? ProductName { get; set; }

    // Optional: Image URL from product lookup
    public string? ImageUrl { get; set; }
}
