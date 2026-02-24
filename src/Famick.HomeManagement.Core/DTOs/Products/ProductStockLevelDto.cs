namespace Famick.HomeManagement.Core.DTOs.Products;

public class ProductStockLevelDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStockAmount { get; set; }
    public string QuantityUnitName { get; set; } = string.Empty;
    public string? ProductGroupName { get; set; }
    public string? ShoppingLocationName { get; set; }
    public StockStatus Status { get; set; }
    public decimal? DaysUntilEmpty { get; set; }  // Based on consumption patterns (future)
}

public enum StockStatus
{
    OK,
    Low,
    OutOfStock
}
