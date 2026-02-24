namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to mark a maintenance schedule as completed
/// </summary>
public class CompleteMaintenanceScheduleRequest
{
    /// <summary>
    /// Date the maintenance was completed (defaults to now)
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Mileage at time of completion
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
}
