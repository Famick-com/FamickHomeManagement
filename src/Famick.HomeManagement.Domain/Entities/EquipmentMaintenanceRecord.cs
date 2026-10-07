namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a completed maintenance activity for equipment.
/// Tracks what maintenance was performed, when, and optionally links to a reminder chore.
/// </summary>
public class EquipmentMaintenanceRecord : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the equipment
    /// </summary>
    public Guid EquipmentId { get; set; }

    /// <summary>
    /// Free-form description of the maintenance performed (e.g., "Oil change", "Filter replaced")
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
    /// Optional notes about the maintenance
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// What the maintenance cost, if recorded
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Who performed the work (garage, contractor, "self")
    /// </summary>
    public string? ServiceProvider { get; set; }

    /// <summary>
    /// Optional linked chore for next maintenance reminder
    /// </summary>
    public Guid? ReminderChoreId { get; set; }

    /// <summary>
    /// The recurring schedule this record satisfies, when it was logged by completing one
    /// </summary>
    public Guid? MaintenanceScheduleId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The equipment this maintenance record belongs to
    /// </summary>
    public virtual Equipment Equipment { get; set; } = null!;

    /// <summary>
    /// The linked reminder chore (optional)
    /// </summary>
    public virtual Chore? ReminderChore { get; set; }

    /// <summary>
    /// The recurring schedule this record was logged against (optional)
    /// </summary>
    public virtual EquipmentMaintenanceSchedule? MaintenanceSchedule { get; set; }

    #endregion
}
