namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// A recurring maintenance schedule for a piece of equipment. Supports time-based intervals
/// (every X months) and usage-based intervals (every X miles/hours/cycles), independently or
/// together.
/// </summary>
/// <remarks>
/// <para>This started life as <c>VehicleMaintenanceSchedule</c> and was generalised when vehicles
/// folded into equipment, so "miles" became whatever <see cref="Equipment.UsageUnit"/> says. It is
/// the only thing in the application that produces maintenance reminders — see
/// <c>UpcomingReminderService</c> and <c>TaskSummaryEvaluator</c>. Equipment other than vehicles
/// had no reminder path at all before this move.</para>
/// </remarks>
public class EquipmentMaintenanceSchedule : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the equipment this schedule belongs to
    /// </summary>
    public Guid EquipmentId { get; set; }

    /// <summary>
    /// Name of the maintenance task (e.g. "Oil Change", "Replace Filter", "Blade Sharpening")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description of what the maintenance involves
    /// </summary>
    public string? Description { get; set; }

    #region Interval Configuration

    /// <summary>
    /// Interval in months (e.g. 3 for every 3 months)
    /// </summary>
    public int? IntervalMonths { get; set; }

    /// <summary>
    /// Interval in the equipment's usage unit (e.g. 5000 for every 5,000 miles, 100 for every
    /// 100 hours). Generalises what used to be a miles-only interval.
    /// </summary>
    public decimal? IntervalUsage { get; set; }

    #endregion

    #region Last Completed

    /// <summary>
    /// Date this maintenance was last completed
    /// </summary>
    public DateTime? LastCompletedDate { get; set; }

    /// <summary>
    /// Usage reading when this maintenance was last completed
    /// </summary>
    public decimal? LastCompletedUsage { get; set; }

    #endregion

    #region Next Due

    /// <summary>
    /// Calculated or manually set next due date
    /// </summary>
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Calculated or manually set next due usage reading
    /// </summary>
    public decimal? NextDueUsage { get; set; }

    #endregion

    /// <summary>
    /// Whether this schedule is active. Inactive schedules produce no reminders.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional notes about this maintenance schedule
    /// </summary>
    public string? Notes { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The equipment this schedule belongs to
    /// </summary>
    public virtual Equipment Equipment { get; set; } = null!;

    /// <summary>
    /// Maintenance records logged against this schedule
    /// </summary>
    public virtual ICollection<EquipmentMaintenanceRecord> MaintenanceRecords { get; set; } = new List<EquipmentMaintenanceRecord>();

    #endregion

    /// <summary>
    /// Recalculates <see cref="NextDueDate"/> from the last completion and the month interval.
    /// </summary>
    public void CalculateNextDueDate()
    {
        if (LastCompletedDate.HasValue && IntervalMonths.HasValue)
        {
            NextDueDate = LastCompletedDate.Value.AddMonths(IntervalMonths.Value);
        }
    }

    /// <summary>
    /// Recalculates <see cref="NextDueUsage"/> from the last completion and the usage interval.
    /// </summary>
    public void CalculateNextDueUsage()
    {
        if (LastCompletedUsage.HasValue && IntervalUsage.HasValue)
        {
            NextDueUsage = LastCompletedUsage.Value + IntervalUsage.Value;
        }
    }

    /// <summary>
    /// Marks the schedule completed and recalculates both next-due values.
    /// </summary>
    /// <param name="completedDate">Date of completion</param>
    /// <param name="completedUsage">Usage reading at completion, if known</param>
    public void MarkCompleted(DateTime completedDate, decimal? completedUsage)
    {
        LastCompletedDate = completedDate;
        LastCompletedUsage = completedUsage;
        CalculateNextDueDate();
        CalculateNextDueUsage();
    }
}
