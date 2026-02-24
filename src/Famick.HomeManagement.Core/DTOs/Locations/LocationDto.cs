namespace Famick.HomeManagement.Core.DTOs.Locations;

/// <summary>
/// Data transfer object for location lookups
/// </summary>
public class LocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
}
