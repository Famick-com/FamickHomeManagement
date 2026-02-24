namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an image associated with a product.
/// Images are stored on the file system; this entity stores metadata.
/// </summary>
public class ProductImage : BaseTenantEntity
{
    /// <summary>
    /// The product this image belongs to.
    /// </summary>
    public Guid ProductId { get; set; }

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
    /// Whether this is the primary/cover image for the product.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// External URL for images from product lookup services (Open Food Facts, etc.).
    /// When set, this URL is used directly instead of local file storage.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Thumbnail URL for external images (smaller version for lists).
    /// </summary>
    public string? ExternalThumbnailUrl { get; set; }

    /// <summary>
    /// Source of the external image (e.g., "openfoodfacts", "usda").
    /// </summary>
    public string? ExternalSource { get; set; }

    // Navigation properties
    public Product Product { get; set; } = null!;
}
