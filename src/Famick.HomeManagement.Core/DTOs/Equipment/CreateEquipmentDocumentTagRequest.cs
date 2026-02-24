namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to create a new equipment document tag
/// </summary>
public class CreateEquipmentDocumentTagRequest
{
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
