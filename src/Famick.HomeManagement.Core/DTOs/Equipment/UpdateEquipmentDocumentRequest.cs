namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to update equipment document metadata
/// </summary>
public class UpdateEquipmentDocumentRequest
{
    public string? DisplayName { get; set; }
    public Guid? TagId { get; set; }
    public int SortOrder { get; set; }
}
