namespace Famick.HomeManagement.Core.DTOs.Locations;

public class CreateLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
