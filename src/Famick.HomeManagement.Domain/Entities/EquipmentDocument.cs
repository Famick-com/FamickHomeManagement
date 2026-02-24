namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a document associated with equipment (manuals, receipts, warranty cards, etc.).
/// Documents are stored on the file system; this entity stores metadata.
/// </summary>
public class EquipmentDocument : BaseTenantEntity
{
    /// <summary>
    /// The equipment this document belongs to
    /// </summary>
    public Guid EquipmentId { get; set; }

    /// <summary>
    /// The stored filename (unique, generated)
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The original filename from the upload
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// The MIME content type (e.g., "application/pdf", "image/jpeg")
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// User-friendly display name for the document
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Display order for sorting documents
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Optional tag for categorizing the document
    /// </summary>
    public Guid? TagId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The equipment this document belongs to
    /// </summary>
    public virtual Equipment Equipment { get; set; } = null!;

    /// <summary>
    /// The tag assigned to this document
    /// </summary>
    public virtual EquipmentDocumentTag? Tag { get; set; }

    #endregion
}
