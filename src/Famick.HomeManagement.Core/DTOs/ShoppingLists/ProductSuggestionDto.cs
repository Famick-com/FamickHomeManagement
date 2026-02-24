namespace Famick.HomeManagement.Core.DTOs.ShoppingLists;

public class ProductSuggestionDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStockAmount { get; set; }
    public decimal SuggestedAmount { get; set; }
    public string Reason { get; set; } = string.Empty; // "Below minimum", "Out of stock"
}
