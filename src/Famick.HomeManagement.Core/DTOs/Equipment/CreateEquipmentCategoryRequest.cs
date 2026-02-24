namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to create a new equipment category
/// </summary>
public class CreateEquipmentCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; }
}
