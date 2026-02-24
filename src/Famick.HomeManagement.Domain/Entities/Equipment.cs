namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a piece of household equipment such as appliances, HVAC systems, etc.
/// Supports hierarchical relationships (e.g., AC unit with attached components).
/// </summary>
public class Equipment : BaseTenantEntity
{
    /// <summary>
    /// Name of the equipment (e.g., "Central AC Unit", "Refrigerator")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Material Design icon name (e.g., "Kitchen", "DirectionsCar", "Hvac")
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Detailed description of the equipment
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Physical location of the equipment (free-text, e.g., "Basement near water heater")
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Manufacturer model number
    /// </summary>
    public string? ModelNumber { get; set; }

    /// <summary>
    /// Serial number for identification and warranty claims
    /// </summary>
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Manufacturer or brand name
    /// </summary>
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Link to manufacturer product page
    /// </summary>
    public string? ManufacturerLink { get; set; }

    /// <summary>
    /// Usage unit for tracking (e.g., "miles", "hours", "cycles").
    /// When set, enables usage tracking UI.
    /// </summary>
    public string? UsageUnit { get; set; }

    /// <summary>
    /// Date the equipment was purchased
    /// </summary>
    public DateTime? PurchaseDate { get; set; }

    /// <summary>
    /// Store or vendor where the equipment was purchased
    /// </summary>
    public string? PurchaseLocation { get; set; }

    /// <summary>
    /// Date when the warranty expires
    /// </summary>
    public DateTime? WarrantyExpirationDate { get; set; }

    /// <summary>
    /// Contact information for warranty claims (phone, email, website)
    /// </summary>
    public string? WarrantyContactInfo { get; set; }

    /// <summary>
    /// Free-form notes for additional information
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Optional category for organizing equipment
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Optional parent equipment for hierarchical relationships
    /// (e.g., Infrared Cleaner attached to AC Unit)
    /// </summary>
    public Guid? ParentEquipmentId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The category this equipment belongs to
    /// </summary>
    public virtual EquipmentCategory? Category { get; set; }

    /// <summary>
    /// The parent equipment if this is a child/component
    /// </summary>
    public virtual Equipment? ParentEquipment { get; set; }

    /// <summary>
    /// Child equipment/components attached to this equipment
    /// </summary>
    public virtual ICollection<Equipment> ChildEquipment { get; set; } = new List<Equipment>();

    /// <summary>
    /// Documents associated with this equipment (manuals, receipts, etc.)
    /// </summary>
    public virtual ICollection<EquipmentDocument> Documents { get; set; } = new List<EquipmentDocument>();

    /// <summary>
    /// Maintenance chores linked to this equipment
    /// </summary>
    public virtual ICollection<Chore> Chores { get; set; } = new List<Chore>();

    /// <summary>
    /// Usage history (odometer, hours, etc.)
    /// </summary>
    public virtual ICollection<EquipmentUsageLog> UsageLogs { get; set; } = new List<EquipmentUsageLog>();

    /// <summary>
    /// Maintenance records for this equipment
    /// </summary>
    public virtual ICollection<EquipmentMaintenanceRecord> MaintenanceRecords { get; set; } = new List<EquipmentMaintenanceRecord>();

    #endregion
}
