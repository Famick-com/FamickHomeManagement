namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to create a maintenance schedule
/// </summary>
public class CreateMaintenanceScheduleRequest
{
    /// <summary>
    /// Name of the maintenance task (e.g., "Oil Change")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the maintenance involves
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Interval in months
    /// </summary>
    public int? IntervalMonths { get; set; }

    /// <summary>
    /// Interval in miles
    /// </summary>
    public int? IntervalMiles { get; set; }

    /// <summary>
    /// Date when maintenance was last completed (optional, for initial setup)
    /// </summary>
    public DateTime? LastCompletedDate { get; set; }

    /// <summary>
    /// Mileage when maintenance was last completed (optional, for initial setup)
    /// </summary>
    public int? LastCompletedMileage { get; set; }

    /// <summary>
    /// Manually set next due date (overrides calculation)
    /// </summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Manually set next due mileage (overrides calculation)
    /// </summary>
    public int? NextDueMileage { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }
}
