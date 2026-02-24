namespace Famick.HomeManagement.Core.DTOs.ProductGroups;

public class ProductSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ProductGroupName { get; set; }
    public string? ShoppingLocationName { get; set; }
}
