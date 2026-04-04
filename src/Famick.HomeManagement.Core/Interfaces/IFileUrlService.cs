namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Consolidates the pattern of resolving a secure download URL for stored files.
/// Handles external URL fallback, HMAC token generation, and URL building in one call.
/// </summary>
public interface IFileUrlService
{
    /// <summary>
    /// Gets the download URL for a product image.
    /// Returns ExternalThumbnailUrl > ExternalUrl > token-signed local URL > null.
    /// </summary>
    string? GetProductImageUrl(Guid productId, Guid imageId, Guid tenantId,
        string? externalThumbnailUrl, string? externalUrl, string? fileName);

    /// <summary>
    /// Gets the download URL for a recipe image.
    /// Returns ExternalUrl > token-signed local URL > null.
    /// </summary>
    string? GetRecipeImageUrl(Guid recipeId, Guid imageId, Guid tenantId,
        string? externalUrl, string? fileName);

    /// <summary>
    /// Gets the download URL for a recipe step image.
    /// Returns ExternalUrl > token-signed local URL > null.
    /// </summary>
    string? GetRecipeStepImageUrl(Guid recipeId, Guid stepId, Guid tenantId,
        string? imageExternalUrl, string? imageFileName);

    /// <summary>
    /// Gets the download URL for a contact profile image (always local).
    /// Returns token-signed local URL if profileImageFileName is present, null otherwise.
    /// </summary>
    string? GetContactProfileImageUrl(Guid contactId, Guid tenantId, string? profileImageFileName);

    /// <summary>
    /// Gets the download URL for an equipment document.
    /// Returns token-signed local URL if fileName is present, null otherwise.
    /// </summary>
    string? GetEquipmentDocumentUrl(Guid documentId, Guid tenantId, string? fileName);

    /// <summary>
    /// Gets the download URL for a storage bin photo.
    /// Returns token-signed local URL if fileName is present, null otherwise.
    /// </summary>
    string? GetStorageBinPhotoUrl(Guid photoId, Guid tenantId, string? fileName);

    /// <summary>
    /// Gets the download URL for a master product image (no token — public/static).
    /// </summary>
    string GetMasterProductImageUrl(Guid masterProductId, Guid imageId);
}
