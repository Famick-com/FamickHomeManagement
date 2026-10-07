using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to create a recurring maintenance schedule.
/// </summary>
public class CreateEquipmentMaintenanceScheduleRequest
{
    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    /// <summary>Interval in months. At least one of the two intervals should be set.</summary>
    [Range(1, 600)]
    public int? IntervalMonths { get; set; }

    /// <summary>Interval in the equipment's usage unit.</summary>
    [Range(0.01, 10_000_000)]
    public decimal? IntervalUsage { get; set; }

    public DateTime? LastCompletedDate { get; set; }

    [Range(0, 100_000_000)]
    public decimal? LastCompletedUsage { get; set; }

    public DateTime? NextDueDate { get; set; }

    [Range(0, 100_000_000)]
    public decimal? NextDueUsage { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
