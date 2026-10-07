namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// A recurring maintenance schedule for a piece of equipment.
/// </summary>
public class EquipmentMaintenanceScheduleDto
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Interval in months, when the schedule is time-based.</summary>
    public int? IntervalMonths { get; set; }

    /// <summary>Interval in the equipment's usage unit, when the schedule is usage-based.</summary>
    public decimal? IntervalUsage { get; set; }

    public DateTime? LastCompletedDate { get; set; }
    public decimal? LastCompletedUsage { get; set; }
    public DateTime? NextDueDate { get; set; }
    public decimal? NextDueUsage { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// The equipment's usage unit, so a client can label <see cref="NextDueUsage"/> without
    /// fetching the parent.
    /// </summary>
    public string? UsageUnit { get; set; }

    /// <summary>Whether this is due now, by either date or usage.</summary>
    public bool IsOverdue { get; set; }

    /// <summary>Whether this falls due soon — within 30 days, or 1000 usage units.</summary>
    public bool IsDueSoon { get; set; }
}
