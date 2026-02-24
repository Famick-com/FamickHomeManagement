namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a physical storage location (kitchen, pantry, freezer, etc.)
/// </summary>
public class Location : BaseTenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    // Navigation properties
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<StorageBin> StorageBins { get; set; } = new List<StorageBin>();
}
