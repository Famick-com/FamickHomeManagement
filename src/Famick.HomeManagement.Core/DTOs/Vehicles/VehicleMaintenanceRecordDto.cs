namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Completed maintenance record data transfer object
/// </summary>
public class VehicleMaintenanceRecordDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public int? MileageAtCompletion { get; set; }
    public decimal? Cost { get; set; }
    public string? ServiceProvider { get; set; }
    public string? Notes { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }
    public string? MaintenanceScheduleName { get; set; }
    public DateTime CreatedAt { get; set; }
}
