namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a user-defined category for organizing equipment.
/// Examples: HVAC, Appliances, Plumbing, Electrical, Outdoor, etc.
/// </summary>
public class EquipmentCategory : BaseTenantEntity
{
    /// <summary>
    /// Name of the category
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the category
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional MudBlazor icon name for display (e.g., "AcUnit", "Kitchen")
    /// </summary>
    public string? IconName { get; set; }

    /// <summary>
    /// Display order for sorting categories
    /// </summary>
    public int SortOrder { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Equipment items in this category
    /// </summary>
    public virtual ICollection<Equipment> Equipment { get; set; } = new List<Equipment>();

    #endregion
}
