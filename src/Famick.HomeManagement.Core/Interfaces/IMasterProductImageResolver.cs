namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Resolves the best available image URL for a master product.
/// Priority: licensed primary image > free static SVG (ImageSlug) > null (caller falls back to IconSvg).
/// </summary>
public interface IMasterProductImageResolver
{
    /// <summary>
    /// Returns the best image URL for a master product, or null if none available.
    /// </summary>
    /// <param name="imageSlug">The static SVG slug (e.g., "whole-milk").</param>
    /// <param name="hasLicensedImage">Whether a licensed image exists for this product.</param>
    /// <param name="masterProductId">The master product ID (for licensed image URL).</param>
    /// <param name="primaryImageId">The primary licensed image ID, if any.</param>
    string? GetImageUrl(string? imageSlug, bool hasLicensedImage, Guid masterProductId, Guid? primaryImageId);
}
