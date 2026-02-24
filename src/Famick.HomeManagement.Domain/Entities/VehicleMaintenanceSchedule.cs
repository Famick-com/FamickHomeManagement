namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a recurring maintenance schedule for a vehicle.
/// Supports both time-based (every X months) and mileage-based (every X miles) intervals.
/// </summary>
public class VehicleMaintenanceSchedule : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the vehicle
    /// </summary>
    public Guid VehicleId { get; set; }

    /// <summary>
    /// Name of the maintenance task (e.g., "Oil Change", "Tire Rotation", "Brake Inspection")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what the maintenance involves
    /// </summary>
    public string? Description { get; set; }

    #region Interval Configuration

    /// <summary>
    /// Interval in months (e.g., 3 for every 3 months)
    /// </summary>
    public int? IntervalMonths { get; set; }

    /// <summary>
    /// Interval in miles (e.g., 5000 for every 5,000 miles)
    /// </summary>
    public int? IntervalMiles { get; set; }

    #endregion

    #region Last Completed

    /// <summary>
    /// Date when this maintenance was last completed
    /// </summary>
    public DateTime? LastCompletedDate { get; set; }

    /// <summary>
    /// Mileage when this maintenance was last completed
    /// </summary>
    public int? LastCompletedMileage { get; set; }

    #endregion

    #region Next Due

    /// <summary>
    /// Calculated or manually set next due date
    /// </summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Calculated or manually set next due mileage
    /// </summary>
    public int? NextDueMileage { get; set; }

    #endregion

    #region Status

    /// <summary>
    /// Whether this schedule is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    #endregion

    /// <summary>
    /// Optional notes about this maintenance schedule
    /// </summary>
    public string? Notes { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The vehicle this schedule belongs to
    /// </summary>
    public virtual Vehicle Vehicle { get; set; } = null!;

    /// <summary>
    /// Maintenance records linked to this schedule
    /// </summary>
    public virtual ICollection<VehicleMaintenanceRecord> MaintenanceRecords { get; set; } = new List<VehicleMaintenanceRecord>();

    #endregion

    /// <summary>
    /// Calculates the next due date based on last completed date and interval
    /// </summary>
    public void CalculateNextDueDate()
    {
        if (LastCompletedDate.HasValue && IntervalMonths.HasValue)
        {
            NextDueDate = LastCompletedDate.Value.AddMonths(IntervalMonths.Value);
        }
    }

    /// <summary>
    /// Calculates the next due mileage based on last completed mileage and interval
    /// </summary>
    public void CalculateNextDueMileage()
    {
        if (LastCompletedMileage.HasValue && IntervalMiles.HasValue)
        {
            NextDueMileage = LastCompletedMileage.Value + IntervalMiles.Value;
        }
    }

    /// <summary>
    /// Marks the schedule as completed and recalculates next due
    /// </summary>
    /// <param name="completedDate">Date of completion</param>
    /// <param name="completedMileage">Mileage at completion</param>
    public void MarkCompleted(DateTime completedDate, int? completedMileage)
    {
        LastCompletedDate = completedDate;
        LastCompletedMileage = completedMileage;
        CalculateNextDueDate();
        CalculateNextDueMileage();
    }
}
