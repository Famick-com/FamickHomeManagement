namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a unit of measurement (kg, pieces, liters, etc.)
/// </summary>
public class QuantityUnit : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string NamePlural { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Product> ProductsWithPurchaseUnit { get; set; } = new List<Product>();
    public ICollection<Product> ProductsWithStockUnit { get; set; } = new List<Product>();
}
