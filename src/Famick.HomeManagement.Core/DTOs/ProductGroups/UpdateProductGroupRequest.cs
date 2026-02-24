namespace Famick.HomeManagement.Core.DTOs.ProductGroups;

public class UpdateProductGroupRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
