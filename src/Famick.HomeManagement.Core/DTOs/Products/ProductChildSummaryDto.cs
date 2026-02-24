namespace Famick.HomeManagement.Core.DTOs.Products;

/// <summary>
/// Lightweight DTO for displaying child products in the parent product detail view.
/// </summary>
public class ProductChildSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalStockAmount { get; set; }
    public string QuantityUnitStockName { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
}
