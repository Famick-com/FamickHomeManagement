namespace Famick.HomeManagement.Core.DTOs.Products;

/// <summary>
/// DTO for product image information.
/// </summary>
public class ProductImageDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Computed URL for displaying the image (for local files).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// External URL for images from product lookup services.
    /// When set, use this URL directly instead of the local Url.
    /// </summary>
    public string? ExternalUrl { get; set; }

    /// <summary>
    /// Thumbnail URL for external images (smaller version for lists).
    /// </summary>
    public string? ExternalThumbnailUrl { get; set; }

    /// <summary>
    /// Source of the external image (e.g., "openfoodfacts").
    /// </summary>
    public string? ExternalSource { get; set; }

    /// <summary>
    /// Gets the display URL (external URL if available, otherwise local URL).
    /// </summary>
    public string DisplayUrl => !string.IsNullOrEmpty(ExternalUrl) ? ExternalUrl : Url;

    /// <summary>
    /// Gets the thumbnail URL for display (external thumbnail if available).
    /// </summary>
    public string ThumbnailDisplayUrl => !string.IsNullOrEmpty(ExternalThumbnailUrl)
        ? ExternalThumbnailUrl
        : DisplayUrl;

    public DateTime CreatedAt { get; set; }
}
