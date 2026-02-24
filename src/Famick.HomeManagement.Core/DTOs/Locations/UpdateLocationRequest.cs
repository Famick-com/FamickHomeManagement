namespace Famick.HomeManagement.Core.DTOs.Locations;

public class UpdateLocationRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
}
