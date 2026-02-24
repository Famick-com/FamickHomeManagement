namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Data transfer object for equipment usage log entries
/// </summary>
public class EquipmentUsageLogDto
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public DateTime Date { get; set; }
    public decimal Reading { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}
