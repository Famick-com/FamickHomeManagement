using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to update a recurring maintenance schedule.
/// </summary>
/// <remarks>
/// Deliberately omits the last-completed values: those are set by completing the schedule, not by
/// editing it, so that the completion history cannot be rewritten by a general-purpose edit.
/// </remarks>
public class UpdateEquipmentMaintenanceScheduleRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Range(1, 600)]
    public int? IntervalMonths { get; set; }

    [Range(0.01, 10_000_000)]
    public decimal? IntervalUsage { get; set; }

    public DateTime? NextDueDate { get; set; }

    [Range(0, 100_000_000)]
    public decimal? NextDueUsage { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(2000)]
    public string? Notes { get; set; }
}
