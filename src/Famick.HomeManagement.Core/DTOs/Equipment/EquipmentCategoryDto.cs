namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Equipment category data transfer object
/// </summary>
public class EquipmentCategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; }
    public int EquipmentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
