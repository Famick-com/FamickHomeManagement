namespace Famick.HomeManagement.Core.DTOs.QuantityUnits;

public class CreateQuantityUnitRequest
{
    public string Name { get; set; } = string.Empty;
    public string NamePlural { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
