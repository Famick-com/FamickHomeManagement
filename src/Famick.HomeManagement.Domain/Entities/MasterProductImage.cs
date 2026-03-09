namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// An image associated with a master product (shared across tenants).
/// </summary>
public class MasterProductImage : BaseEntity
{
    public Guid MasterProductId { get; set; }

    /// <summary>
    /// The stored filename (unique, generated).
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The original filename from the upload.
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// The MIME content type (e.g., "image/jpeg").
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Display order for sorting images.
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this is the primary/cover image.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// External URL for images from product lookup services.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Thumbnail URL for external images.
    /// </summary>
    public string? ExternalThumbnailUrl { get; set; }

    /// <summary>
    /// Source of the external image (e.g., "openfoodfacts", "usda").
    /// </summary>
    public string? ExternalSource { get; set; }

    // Navigation properties
    public MasterProduct MasterProduct { get; set; } = null!;
}
