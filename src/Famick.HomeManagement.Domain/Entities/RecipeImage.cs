namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an image associated with a recipe.
/// Images are stored on the file system; this entity stores metadata.
/// Follows the same pattern as ProductImage.
/// </summary>
public class RecipeImage : BaseTenantEntity
{
    /// <summary>
    /// The recipe this image belongs to
    /// </summary>
    public Guid RecipeId { get; set; }

    /// <summary>
    /// The stored filename (unique, generated)
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// The original filename from the upload
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// The MIME content type (e.g., "image/jpeg")
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Display order for sorting images
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether this is the primary/cover image for the recipe
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// External URL for images from external sources.
    /// When set, this URL is used directly instead of local file storage.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Thumbnail URL for external images (smaller version for lists)
    /// </summary>
    public string? ExternalThumbnailUrl { get; set; }

    /// <summary>
    /// Source of the external image (e.g., "import", "url")
    /// </summary>
    public string? ExternalSource { get; set; }

    // Navigation properties

    /// <summary>
    /// The recipe this image belongs to
    /// </summary>
    public virtual Recipe Recipe { get; set; } = null!;
}
