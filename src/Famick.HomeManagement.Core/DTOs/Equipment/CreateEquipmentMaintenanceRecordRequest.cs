namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Request model for creating a new equipment maintenance record
/// </summary>
public class CreateEquipmentMaintenanceRecordRequest
{
    /// <summary>
    /// Description of the maintenance performed (e.g., "Oil change", "Filter replaced")
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Date the maintenance was completed
    /// </summary>
    public DateTime CompletedDate { get; set; }

    /// <summary>
    /// Usage meter reading at time of maintenance (optional)
    /// </summary>
    public decimal? UsageAtCompletion { get; set; }

    /// <summary>
    /// What the maintenance cost (optional)
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Who performed the work (optional)
    /// </summary>
    public string? ServiceProvider { get; set; }

    /// <summary>
    /// Optional notes about the maintenance
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// The recurring schedule this record satisfies (optional)
    /// </summary>
    public Guid? MaintenanceScheduleId { get; set; }

    /// <summary>
    /// Whether to create a reminder chore for the next maintenance
    /// </summary>
    public bool CreateReminder { get; set; }

    /// <summary>
    /// Name for the reminder chore (defaults to Description if not set)
    /// </summary>
    public string? ReminderName { get; set; }

    /// <summary>
    /// Due date for the reminder chore
    /// </summary>
    public DateTime? ReminderDueDate { get; set; }
}
