namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a photo attached to a storage bin.
/// Photos are stored on the file system; this entity stores metadata.
/// </summary>
public class StorageBinPhoto : BaseTenantEntity
{
    /// <summary>
    /// The storage bin this photo belongs to.
    /// </summary>
    public Guid StorageBinId { get; set; }

    /// <summary>
    /// The stored filename (unique, generated).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The original filename from the upload.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// The MIME content type (e.g., "image/jpeg", "image/png").
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Display order for sorting photos.
    /// </summary>
    public int SortOrder { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The storage bin this photo belongs to.
    /// </summary>
    public virtual StorageBin StorageBin { get; set; } = null!;

    #endregion
}
