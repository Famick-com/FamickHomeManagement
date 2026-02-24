namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to update an existing equipment document tag
/// </summary>
public class UpdateEquipmentDocumentTagRequest
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
