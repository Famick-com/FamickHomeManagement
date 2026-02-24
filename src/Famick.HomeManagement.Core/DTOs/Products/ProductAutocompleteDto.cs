namespace Famick.HomeManagement.Core.DTOs.Products;

public class ProductAutocompleteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductGroupName { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public string? PreferredStoreAisle { get; set; }
    public string? PreferredStoreDepartment { get; set; }
}
