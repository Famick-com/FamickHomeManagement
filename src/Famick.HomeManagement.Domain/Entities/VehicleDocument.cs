namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a document associated with a vehicle (registration, insurance, title, etc.).
/// Documents are stored on the file system; this entity stores metadata.
/// </summary>
public class VehicleDocument : BaseTenantEntity
{
    /// <summary>
    /// Foreign key to the vehicle
    /// </summary>
    public Guid VehicleId { get; set; }

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
    /// Document type/category (e.g., "Registration", "Insurance", "Title", "Service Receipt")
    /// </summary>
    public string? DocumentType { get; set; }

    /// <summary>
    /// Expiration date for time-sensitive documents (registration, insurance)
    /// </summary>
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Display order for sorting documents
    /// </summary>
    public int SortOrder { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The vehicle this document belongs to
    /// </summary>
    public virtual Vehicle Vehicle { get; set; } = null!;

    #endregion
}
