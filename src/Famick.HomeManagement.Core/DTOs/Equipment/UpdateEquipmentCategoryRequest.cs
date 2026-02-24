namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to update an existing equipment category
/// </summary>
public class UpdateEquipmentCategoryRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; }
}
