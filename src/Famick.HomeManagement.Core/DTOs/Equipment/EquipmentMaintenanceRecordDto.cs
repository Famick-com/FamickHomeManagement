namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Data transfer object for equipment maintenance records
/// </summary>
public class EquipmentMaintenanceRecordDto
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public decimal? UsageAtCompletion { get; set; }
    public decimal? Cost { get; set; }
    public string? ServiceProvider { get; set; }
    public string? Notes { get; set; }
    public Guid? ReminderChoreId { get; set; }
    public Guid? MaintenanceScheduleId { get; set; }

    /// <summary>
    /// Name of the schedule this record was logged against (populated from navigation)
    /// </summary>
    public string? MaintenanceScheduleName { get; set; }

    /// <summary>
    /// Name of the linked reminder chore (populated from navigation)
    /// </summary>
    public string? ReminderChoreName { get; set; }

    public DateTime CreatedAt { get; set; }
}
