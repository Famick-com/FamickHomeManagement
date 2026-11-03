using Famick.HomeManagement.Domain.Enums;

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

    /// <summary>
    /// The standard measure this unit is, if any, so amounts in it convert to other units of the same
    /// kind. Null for units with no fixed size, such as Piece, Bag or Pinch.
    /// </summary>
    public StandardUnit? StandardUnit { get; set; }

    // Navigation properties
    public ICollection<Product> ProductsWithPurchaseUnit { get; set; } = new List<Product>();
    public ICollection<Product> ProductsWithStockUnit { get; set; } = new List<Product>();
}
