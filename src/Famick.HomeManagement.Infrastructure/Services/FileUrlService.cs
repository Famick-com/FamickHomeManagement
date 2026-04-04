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
        return fileStorage.GetRecipeStepImageUrl(recipeId, stepId, token);
    }

    public string? GetContactProfileImageUrl(Guid contactId, Guid tenantId, string? profileImageFileName)
    {
        if (string.IsNullOrEmpty(profileImageFileName)) return null;

        var token = tokenService.GenerateToken("contact-profile-image", contactId, tenantId);
        return fileStorage.GetContactProfileImageUrl(contactId, token);
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
}
