namespace Famick.HomeManagement.Core.DTOs.ProductGroups;

public class CreateProductGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
