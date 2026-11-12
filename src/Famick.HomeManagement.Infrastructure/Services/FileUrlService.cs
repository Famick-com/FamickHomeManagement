using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Consolidates file access token generation and URL building for all resource types.
/// Wraps IFileAccessTokenService + IFileStorageService so callers never need to
/// generate tokens or construct URLs directly.
/// </summary>
public class FileUrlService(
    IFileStorageService fileStorage,
    IFileAccessTokenService tokenService) : IFileUrlService
{
    public string? GetProductImageUrl(Guid productId, Guid imageId, Guid tenantId,
        string? externalThumbnailUrl, string? externalUrl, string? fileName)
    {
        if (!string.IsNullOrEmpty(externalThumbnailUrl)) return externalThumbnailUrl;
        if (!string.IsNullOrEmpty(externalUrl)) return externalUrl;
        if (string.IsNullOrEmpty(fileName)) return null;

        var token = tokenService.GenerateToken("product-image", imageId, tenantId);
        return fileStorage.GetProductImageUrl(productId, imageId, token);
    }

    public string? GetRecipeImageUrl(Guid recipeId, Guid imageId, Guid tenantId,
        string? externalUrl, string? fileName)
    {
        if (!string.IsNullOrEmpty(externalUrl)) return externalUrl;
        if (string.IsNullOrEmpty(fileName)) return null;

        var token = tokenService.GenerateToken("recipe-image", imageId, tenantId);
        return fileStorage.GetRecipeImageUrl(recipeId, imageId, token);
    }

    public string? GetRecipeStepImageUrl(Guid recipeId, Guid stepId, Guid tenantId,
        string? imageExternalUrl, string? imageFileName)
    {
        if (!string.IsNullOrEmpty(imageExternalUrl)) return imageExternalUrl;
        if (string.IsNullOrEmpty(imageFileName)) return null;

        var token = tokenService.GenerateToken("recipe-step-image", stepId, tenantId);
        return WithVersion(fileStorage.GetRecipeStepImageUrl(recipeId, stepId, token), imageFileName);
    }

    public string? GetContactProfileImageUrl(Guid contactId, Guid tenantId, string? profileImageFileName)
    {
        if (string.IsNullOrEmpty(profileImageFileName)) return null;

        var token = tokenService.GenerateToken("contact-profile-image", contactId, tenantId);
        return WithVersion(fileStorage.GetContactProfileImageUrl(contactId, token), profileImageFileName);
    }

    public string? GetEquipmentDocumentUrl(Guid documentId, Guid tenantId, string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        var token = tokenService.GenerateToken("equipment-document", documentId, tenantId);
        return fileStorage.GetEquipmentDocumentUrl(documentId, token);
    }

    public string? GetStorageBinPhotoUrl(Guid photoId, Guid tenantId, string? fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return null;

        var token = tokenService.GenerateToken("storage-bin-photo", photoId, tenantId);
        return fileStorage.GetStorageBinPhotoUrl(photoId, token);
    }

    public string GetMasterProductImageUrl(Guid masterProductId, Guid imageId)
    {
        return fileStorage.GetMasterProductImageUrl(masterProductId, imageId);
    }

    /// <summary>
    /// Adds a version to a URL whose path is fixed per owner rather than per file.
    ///
    /// Stored files are served with a one-year immutable cache lifetime, which is
    /// only safe while replacing a file changes its URL. Product, recipe and bin
    /// images get a new id per upload, so theirs does. A contact has one profile
    /// image URL and a recipe step one image URL however many times the file is
    /// replaced, so without this a browser kept showing the old image until the
    /// token rolled over.
    ///
    /// The stored filename is unique per upload, so a short hash of it changes
    /// exactly when the content does. The endpoint ignores the parameter.
    /// </summary>
    private static string WithVersion(string url, string fileName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(fileName));
        var version = Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}v={version}";
    }
}
