namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a tag for categorizing equipment documents.
/// Includes default tags (Manual, Receipt, Warranty Card, etc.) and user-defined tags.
/// </summary>
public class EquipmentDocumentTag : BaseTenantEntity
{
    /// <summary>
    /// Name of the tag
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a system-seeded default tag
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Display order for sorting tags
    /// </summary>
    public int SortOrder { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Documents using this tag
    /// </summary>
    public virtual ICollection<EquipmentDocument> Documents { get; set; } = new List<EquipmentDocument>();

    #endregion
}
