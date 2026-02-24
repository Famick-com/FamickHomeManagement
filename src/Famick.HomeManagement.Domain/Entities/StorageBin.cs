namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a labeled storage bin/container with QR code identification.
/// Contains a markdown description of contents and optional photos.
/// </summary>
public class StorageBin : BaseTenantEntity
{
    /// <summary>
    /// Human-readable short code used in QR code URLs (e.g., "blue-oak-47").
    /// Format: adjective-noun-number. Unique within tenant.
    /// </summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>
    /// Markdown description of the bin contents.
    /// Supports basic formatting: bold, italic, lists, headers.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional location where this storage bin is physically kept.
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Freeform category for organizing bins (e.g., "Holiday Decorations", "Tools").
    /// </summary>
    public string? Category { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The location where this bin is stored (optional).
    /// </summary>
    public virtual Location? Location { get; set; }

    /// <summary>
    /// Photos attached to this storage bin (images of contents).
    /// </summary>
    public virtual ICollection<StorageBinPhoto> Photos { get; set; } = new List<StorageBinPhoto>();

    #endregion
}
