using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request to mark a maintenance schedule done, which logs a maintenance record and rolls the
/// schedule forward to its next due date/usage.
/// </summary>
public class CompleteEquipmentMaintenanceScheduleRequest
{
    /// <summary>When the work was done. Defaults to now when omitted.</summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Usage reading at completion. Defaults to the equipment's latest usage reading when omitted,
    /// so a usage-based schedule rolls forward correctly without the caller having to restate it.
    /// </summary>
    [Range(0, 100_000_000)]
    public decimal? UsageAtCompletion { get; set; }

    [Range(0, 1_000_000)]
    public decimal? Cost { get; set; }

    [StringLength(200)]
    public string? ServiceProvider { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}
