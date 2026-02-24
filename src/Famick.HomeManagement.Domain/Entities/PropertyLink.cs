namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a URL link associated with a home/property.
/// Used to store links to external resources like county records, Zillow, HOA portals, etc.
/// </summary>
public class PropertyLink : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the home
    /// </summary>
    public Guid HomeId { get; set; }

    /// <summary>
    /// The URL of the link
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// User-defined label for the link (e.g., "County Records", "Zillow Listing", "HOA Portal")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting links
    /// </summary>
    public int SortOrder { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The home this link belongs to
    /// </summary>
    public virtual Home Home { get; set; } = null!;

    #endregion
}
