namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to add a document to equipment (metadata only, file is sent separately)
/// </summary>
public class AddEquipmentDocumentRequest
{
    public string? DisplayName { get; set; }
    public Guid? TagId { get; set; }
}
