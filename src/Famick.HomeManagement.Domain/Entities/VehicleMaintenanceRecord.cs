namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a completed maintenance activity for a vehicle.
/// Tracks what maintenance was performed, when, at what mileage, and optionally the cost.
/// </summary>
public class VehicleMaintenanceRecord : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the vehicle
    /// </summary>
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Description of the maintenance performed (e.g., "Oil change", "Tire rotation")
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
    /// Optional notes about the maintenance
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional link to maintenance schedule that triggered this record
    /// </summary>
    public Guid? MaintenanceScheduleId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The vehicle this maintenance record belongs to
    /// </summary>
    public virtual Vehicle Vehicle { get; set; } = null!;

    /// <summary>
    /// The maintenance schedule that was completed (optional)
    /// </summary>
    public virtual VehicleMaintenanceSchedule? MaintenanceSchedule { get; set; }

    #endregion
}
