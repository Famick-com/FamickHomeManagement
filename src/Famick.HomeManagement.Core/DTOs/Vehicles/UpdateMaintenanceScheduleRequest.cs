namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Request to update a maintenance schedule
/// </summary>
public class UpdateMaintenanceScheduleRequest
{
    /// <summary>
    /// Name of the maintenance task
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
    /// Manually set next due date
    /// </summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Manually set next due mileage
    /// </summary>
    public int? NextDueMileage { get; set; }

    /// <summary>
    /// Whether the schedule is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }
}
