namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Maintenance schedule data transfer object
/// </summary>
public class VehicleMaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    #region Interval Configuration

    public int? IntervalMonths { get; set; }
    public int? IntervalMiles { get; set; }

    #endregion

    #region Last Completed

    public DateTime? LastCompletedDate { get; set; }
    public int? LastCompletedMileage { get; set; }

    #endregion

    #region Next Due

    public DateTime? NextDueDate { get; set; }
    public int? NextDueMileage { get; set; }

    #endregion

    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Indicates if maintenance is overdue (past due date or mileage)
    /// </summary>
    public bool IsOverdue { get; set; }

    /// <summary>
    /// Indicates if maintenance is due soon (within 30 days or 1000 miles)
    /// </summary>
    public bool IsDueSoon { get; set; }
}
