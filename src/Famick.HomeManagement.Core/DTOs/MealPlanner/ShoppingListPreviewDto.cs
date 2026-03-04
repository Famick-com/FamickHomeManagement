namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class ShoppingListPreviewDto
{
    public List<ShoppingListPreviewItemDto> NeededItems { get; set; } = new();
    public List<ShoppingListPreviewItemDto> InStockItems { get; set; } = new();
    public List<string> UntrackedItems { get; set; } = new();
    public List<ShoppingListPreviewItemDto> BatchCoveredItems { get; set; } = new();
}

public class ShoppingListPreviewItemDto
{
    public Guid? ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? QuantityUnitName { get; set; }
    public decimal? CurrentStock { get; set; }
    public decimal? BatchCoveredQuantity { get; set; }
    public string? BatchSourceDescription { get; set; }
}
