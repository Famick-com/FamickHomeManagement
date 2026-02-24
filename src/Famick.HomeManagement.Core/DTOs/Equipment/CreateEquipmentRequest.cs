namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to create a new equipment item
/// </summary>
public class CreateEquipmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerLink { get; set; }
    public string? UsageUnit { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public DateTime? WarrantyExpirationDate { get; set; }
    public string? WarrantyContactInfo { get; set; }
    public string? Notes { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ParentEquipmentId { get; set; }
}
