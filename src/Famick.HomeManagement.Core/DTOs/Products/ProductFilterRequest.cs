namespace Famick.HomeManagement.Core.DTOs.Products;

public class ProductFilterRequest
{
    public string? SearchTerm { get; set; }
    public Guid? LocationId { get; set; }
    public Guid? ProductGroupId { get; set; }  // Phase 2
    public Guid? ShoppingLocationId { get; set; }  // Phase 2
    public bool? IsActive { get; set; }
    public bool? LowStock { get; set; }  // Phase 2: Filter products below MinStockAmount
    public string? SortBy { get; set; } = "Name";
    public bool Descending { get; set; } = false;
}
