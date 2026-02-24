namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to log a completed maintenance record
/// </summary>
public class CreateMaintenanceRecordRequest
{
    /// <summary>
    /// Description of the maintenance performed
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Date the maintenance was completed
    /// </summary>
    public DateTime CompletedDate { get; set; }

    /// <summary>
    /// Odometer reading at time of maintenance
    /// </summary>
    public int? MileageAtCompletion { get; set; }

    /// <summary>
    /// Cost of the maintenance
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Name of the service provider/shop
    /// </summary>
    public string? ServiceProvider { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional link to the maintenance schedule this fulfills
    /// </summary>
    public Guid? MaintenanceScheduleId { get; set; }
}
