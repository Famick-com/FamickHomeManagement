namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a usage reading for equipment (odometer, hours, cycles, etc.).
/// Used to track usage history over time.
/// </summary>
public class EquipmentUsageLog : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the equipment
    /// </summary>
    public Guid EquipmentId { get; set; }

    /// <summary>
    /// Date of the usage reading
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Usage reading value (miles, hours, cycles, etc.)
    /// </summary>
    public decimal Reading { get; set; }

    /// <summary>
    /// Optional notes about this reading
    /// </summary>
    public string? Notes { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The equipment this usage log belongs to
    /// </summary>
    public virtual Equipment Equipment { get; set; } = null!;

    #endregion
}
