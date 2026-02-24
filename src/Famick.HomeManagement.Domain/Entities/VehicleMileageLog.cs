namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Tracks odometer readings over time for a vehicle.
/// Used to calculate average mileage, predict maintenance needs, etc.
/// </summary>
public class VehicleMileageLog : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the vehicle
    /// </summary>
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Odometer reading at time of logging
    /// </summary>
    public int Mileage { get; set; }

    /// <summary>
    /// Date of the reading
    /// </summary>
    public DateTime ReadingDate { get; set; }

    /// <summary>
    /// Optional notes (e.g., "At oil change", "Trip start")
    /// </summary>
    public string? Notes { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The vehicle this mileage log belongs to
    /// </summary>
    public virtual Vehicle Vehicle { get; set; } = null!;

    #endregion
}
