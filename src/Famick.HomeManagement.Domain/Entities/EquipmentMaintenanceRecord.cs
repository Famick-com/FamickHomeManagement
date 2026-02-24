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
    /// Optional linked chore for next maintenance reminder
    /// </summary>
    public Guid? ReminderChoreId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The equipment this maintenance record belongs to
    /// </summary>
    public virtual Equipment Equipment { get; set; } = null!;

    /// <summary>
    /// The linked reminder chore (optional)
    /// </summary>
    public virtual Chore? ReminderChore { get; set; }

    #endregion
}
