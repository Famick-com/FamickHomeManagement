using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// File storage service that saves files to the local file system.
/// Files are stored outside wwwroot in {contentRoot}/uploads/{type}/{id}/{filename}
/// to prevent direct access - all files must be served through authenticated API endpoints.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly string _baseUrl;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly HttpClient _httpClient;

    public LocalFileStorageService(
        string contentRootPath,
        string baseUrl,
        ILogger<LocalFileStorageService> logger,
        IHttpClientFactory? httpClientFactory = null)
    {
        _basePath = Path.Combine(contentRootPath, "uploads");
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _httpClient = httpClientFactory?.CreateClient("ImageDownloader") ?? new HttpClient();

        // Ensure base uploads directory exists
        Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveProductImageAsync(Guid productId, Stream stream, string fileName, CancellationToken ct = default)
    {
        var directory = GetProductImageDirectory(productId);
        Directory.CreateDirectory(directory);

        var uniqueFileName = GenerateUniqueFileName(fileName);
        var filePath = Path.Combine(directory, uniqueFileName);

        try
        {
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Saved product image {FileName} for product {ProductId}", uniqueFileName, productId);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save product image {FileName} for product {ProductId}", fileName, productId);
            throw;
        }
    }

    public Task DeleteProductImageAsync(Guid productId, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetProductImageDirectory(productId), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted product image {FileName} for product {ProductId}", fileName, productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete product image {FileName} for product {ProductId}", fileName, productId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public string GetProductImageUrl(Guid productId, Guid imageId, string? accessToken = null)
    {
        var url = $"{_baseUrl}/api/v1/products/{productId}/images/{imageId}/download";
        return string.IsNullOrEmpty(accessToken) ? url : $"{url}?token={accessToken}";
    }

    public string GetProductImagePath(Guid productId, string fileName)
    {
        return Path.Combine(GetProductImageDirectory(productId), fileName);
    }

    public Task<Stream?> GetProductImageStreamAsync(Guid productId, string fileName, CancellationToken ct = default)
    {
        var filePath = GetProductImagePath(productId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    public Task DeleteAllProductImagesAsync(Guid productId, CancellationToken ct = default)
    {
        var directory = GetProductImageDirectory(productId);

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                _logger.LogInformation("Deleted all images for product {ProductId}", productId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image directory for product {ProductId}", productId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public async Task<string?> DownloadAndSaveProductImageAsync(
        Guid productId,
        string imageUrl,
        string source,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        try
        {
            // Download the image
            using var response = await _httpClient.GetAsync(imageUrl, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to download image from {Url}: {StatusCode}", imageUrl, response.StatusCode);
                return null;
            }

            // Determine file extension from content type or URL
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var extension = GetExtensionFromContentType(contentType) ?? GetExtensionFromUrl(imageUrl) ?? ".jpg";

            // Generate filename with source prefix
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var random = Guid.NewGuid().ToString("N")[..8];
            var fileName = $"{source}_{timestamp}_{random}{extension}";

            // Save the image
            var directory = GetProductImageDirectory(productId);
            Directory.CreateDirectory(directory);

            var filePath = Path.Combine(directory, fileName);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Downloaded and saved product image {FileName} from {Source} for product {ProductId}",
                fileName, source, productId);

            return fileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and save product image from {Url} for product {ProductId}", imageUrl, productId);
            return null;
        }
    }

    private static string? GetExtensionFromContentType(string? contentType)
    {
        return contentType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/svg+xml" => ".svg",
            "image/bmp" => ".bmp",
            _ => null
        };
    }

    private static string? GetExtensionFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var path = uri.AbsolutePath;
            var extension = Path.GetExtension(path);
            return string.IsNullOrEmpty(extension) ? null : extension.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private string GetProductImageDirectory(Guid productId)
    {
        return Path.Combine(_basePath, "products", productId.ToString());
    }

    private static string GenerateUniqueFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"image_{timestamp}_{random}{extension}";
    }

    #region Equipment Documents

    public async Task<string> SaveEquipmentDocumentAsync(Guid equipmentId, Stream stream, string fileName, CancellationToken ct = default)
    {
        var directory = GetEquipmentDocumentDirectory(equipmentId);
        Directory.CreateDirectory(directory);

        var uniqueFileName = GenerateUniqueDocumentFileName(fileName);
        var filePath = Path.Combine(directory, uniqueFileName);

        try
        {
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Saved equipment document {FileName} for equipment {EquipmentId}", uniqueFileName, equipmentId);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save equipment document {FileName} for equipment {EquipmentId}", fileName, equipmentId);
            throw;
        }
    }

    public Task DeleteEquipmentDocumentAsync(Guid equipmentId, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetEquipmentDocumentDirectory(equipmentId), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted equipment document {FileName} for equipment {EquipmentId}", fileName, equipmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete equipment document {FileName} for equipment {EquipmentId}", fileName, equipmentId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public string GetEquipmentDocumentUrl(Guid documentId, string? accessToken = null)
    {
        var url = $"{_baseUrl}/api/v1/equipment/documents/{documentId}/download";
        return string.IsNullOrEmpty(accessToken) ? url : $"{url}?token={accessToken}";
    }

    public string GetEquipmentDocumentPath(Guid equipmentId, string fileName)
    {
        return Path.Combine(GetEquipmentDocumentDirectory(equipmentId), fileName);
    }

    public Task<Stream?> GetEquipmentDocumentStreamAsync(Guid equipmentId, string fileName, CancellationToken ct = default)
    {
        var filePath = GetEquipmentDocumentPath(equipmentId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    public Task DeleteAllEquipmentDocumentsAsync(Guid equipmentId, CancellationToken ct = default)
    {
        var directory = GetEquipmentDocumentDirectory(equipmentId);

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                _logger.LogInformation("Deleted all documents for equipment {EquipmentId}", equipmentId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete document directory for equipment {EquipmentId}", equipmentId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    private string GetEquipmentDocumentDirectory(Guid equipmentId)
    {
        return Path.Combine(_basePath, "equipment", equipmentId.ToString());
    }

    private static string GenerateUniqueDocumentFileName(string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"doc_{timestamp}_{random}{extension}";
    }

    #endregion

    #region Storage Bin Photos

    public async Task<string> SaveStorageBinPhotoAsync(Guid storageBinId, Stream stream, string fileName, string contentType, CancellationToken ct = default)
    {
        var directory = GetStorageBinPhotoDirectory(storageBinId);
        Directory.CreateDirectory(directory);

        var uniqueFileName = GenerateUniquePhotoFileName(fileName, contentType);
        var filePath = Path.Combine(directory, uniqueFileName);

        try
        {
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Saved storage bin photo {FileName} for bin {StorageBinId}", uniqueFileName, storageBinId);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save storage bin photo {FileName} for bin {StorageBinId}", fileName, storageBinId);
            throw;
        }
    }

    public Task DeleteStorageBinPhotoAsync(Guid storageBinId, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetStorageBinPhotoDirectory(storageBinId), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted storage bin photo {FileName} for bin {StorageBinId}", fileName, storageBinId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete storage bin photo {FileName} for bin {StorageBinId}", fileName, storageBinId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public string GetStorageBinPhotoUrl(Guid photoId, string? accessToken = null)
    {
        var url = $"{_baseUrl}/api/v1/storage-bins/photos/{photoId}/download";
        return string.IsNullOrEmpty(accessToken) ? url : $"{url}?token={accessToken}";
    }

    public string GetStorageBinPhotoPath(Guid storageBinId, string fileName)
    {
        return Path.Combine(GetStorageBinPhotoDirectory(storageBinId), fileName);
    }

    public Task<Stream?> GetStorageBinPhotoStreamAsync(Guid storageBinId, string fileName, CancellationToken ct = default)
    {
        var filePath = GetStorageBinPhotoPath(storageBinId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    public Task DeleteAllStorageBinPhotosAsync(Guid storageBinId, CancellationToken ct = default)
    {
        var directory = GetStorageBinPhotoDirectory(storageBinId);

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                _logger.LogInformation("Deleted all photos for storage bin {StorageBinId}", storageBinId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete photo directory for storage bin {StorageBinId}", storageBinId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    private string GetStorageBinPhotoDirectory(Guid storageBinId)
    {
        return Path.Combine(_basePath, "storage-bins", storageBinId.ToString());
    }

    private static string GenerateUniquePhotoFileName(string originalFileName, string contentType)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        // If filename has no extension, derive it from content type
        if (string.IsNullOrEmpty(extension))
        {
            extension = GetExtensionFromContentType(contentType) ?? ".jpg";
        }

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var random = Guid.NewGuid().ToString("N")[..8];
        return $"photo_{timestamp}_{random}{extension}";
    }

    #endregion

    #region Recipe Images

    public async Task<string> SaveRecipeImageAsync(Guid recipeId, Stream stream, string fileName, CancellationToken ct = default)
    {
        var directory = GetRecipeImageDirectory(recipeId);
        Directory.CreateDirectory(directory);

        var uniqueFileName = GenerateUniqueFileName(fileName);
        var filePath = Path.Combine(directory, uniqueFileName);

        try
        {
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Saved recipe image {FileName} for recipe {RecipeId}", uniqueFileName, recipeId);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recipe image {FileName} for recipe {RecipeId}", fileName, recipeId);
            throw;
        }
    }

    public Task DeleteRecipeImageAsync(Guid recipeId, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetRecipeImageDirectory(recipeId), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted recipe image {FileName} for recipe {RecipeId}", fileName, recipeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete recipe image {FileName} for recipe {RecipeId}", fileName, recipeId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public string GetRecipeImageUrl(Guid recipeId, Guid imageId, string? accessToken = null)
    {
        var url = $"{_baseUrl}/api/v1/recipes/{recipeId}/images/{imageId}/download";
        return string.IsNullOrEmpty(accessToken) ? url : $"{url}?token={accessToken}";
    }

    public string GetRecipeImagePath(Guid recipeId, string fileName)
    {
        return Path.Combine(GetRecipeImageDirectory(recipeId), fileName);
    }

    public Task<Stream?> GetRecipeImageStreamAsync(Guid recipeId, string fileName, CancellationToken ct = default)
    {
        var filePath = GetRecipeImagePath(recipeId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    public Task DeleteAllRecipeImagesAsync(Guid recipeId, CancellationToken ct = default)
    {
        var directory = GetRecipeImageDirectory(recipeId);

        if (Directory.Exists(directory))
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                _logger.LogInformation("Deleted all images for recipe {RecipeId}", recipeId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete image directory for recipe {RecipeId}", recipeId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    private string GetRecipeImageDirectory(Guid recipeId)
    {
        return Path.Combine(_basePath, "recipe-images", recipeId.ToString());
    }

    #endregion

    #region Recipe Step Images

    public async Task<string> SaveRecipeStepImageAsync(Guid recipeId, Guid stepId, Stream stream, string fileName, CancellationToken ct = default)
    {
        var directory = GetRecipeStepImageDirectory(recipeId, stepId);
        Directory.CreateDirectory(directory);

        var uniqueFileName = GenerateUniqueFileName(fileName);
        var filePath = Path.Combine(directory, uniqueFileName);

        try
        {
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Saved recipe step image {FileName} for recipe {RecipeId} step {StepId}", uniqueFileName, recipeId, stepId);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save recipe step image {FileName} for recipe {RecipeId} step {StepId}", fileName, recipeId, stepId);
            throw;
        }
    }

    public Task DeleteRecipeStepImageAsync(Guid recipeId, Guid stepId, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetRecipeStepImageDirectory(recipeId, stepId), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted recipe step image {FileName} for recipe {RecipeId} step {StepId}", fileName, recipeId, stepId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete recipe step image {FileName} for recipe {RecipeId} step {StepId}", fileName, recipeId, stepId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public string GetRecipeStepImageUrl(Guid recipeId, Guid stepId, string? accessToken = null)
    {
        var url = $"{_baseUrl}/api/v1/recipes/{recipeId}/steps/{stepId}/image/download";
        return string.IsNullOrEmpty(accessToken) ? url : $"{url}?token={accessToken}";
    }

    public string GetRecipeStepImagePath(Guid recipeId, Guid stepId, string fileName)
    {
        return Path.Combine(GetRecipeStepImageDirectory(recipeId, stepId), fileName);
    }

    public Task<Stream?> GetRecipeStepImageStreamAsync(Guid recipeId, Guid stepId, string fileName, CancellationToken ct = default)
    {
        var filePath = GetRecipeStepImagePath(recipeId, stepId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    private string GetRecipeStepImageDirectory(Guid recipeId, Guid stepId)
    {
        return Path.Combine(_basePath, "recipe-step-images", recipeId.ToString(), stepId.ToString());
    }

    #endregion

    #region Contact Profile Images

    public async Task<string> SaveContactProfileImageAsync(Guid contactId, Stream stream, string fileName, CancellationToken ct = default)
    {
        var directory = GetContactProfileImageDirectory(contactId);
        Directory.CreateDirectory(directory);

        var uniqueFileName = GenerateUniqueFileName(fileName);
        var filePath = Path.Combine(directory, uniqueFileName);

        try
        {
            await using var fileStream = File.Create(filePath);
            await stream.CopyToAsync(fileStream, ct);

            _logger.LogInformation("Saved contact profile image {FileName} for contact {ContactId}", uniqueFileName, contactId);
            return uniqueFileName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save contact profile image {FileName} for contact {ContactId}", fileName, contactId);
            throw;
        }
    }

    public Task DeleteContactProfileImageAsync(Guid contactId, string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(GetContactProfileImageDirectory(contactId), fileName);

        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted contact profile image {FileName} for contact {ContactId}", fileName, contactId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete contact profile image {FileName} for contact {ContactId}", fileName, contactId);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public string GetContactProfileImageUrl(Guid contactId, string? accessToken = null)
    {
        var url = $"{_baseUrl}/api/v1/contacts/{contactId}/profile-image";
        return string.IsNullOrEmpty(accessToken) ? url : $"{url}?token={accessToken}";
    }

    public string GetContactProfileImagePath(Guid contactId, string fileName)
    {
        return Path.Combine(GetContactProfileImageDirectory(contactId), fileName);
    }

    public Task<Stream?> GetContactProfileImageStreamAsync(Guid contactId, string fileName, CancellationToken ct = default)
    {
        var filePath = GetContactProfileImagePath(contactId, fileName);
        if (!File.Exists(filePath))
            return Task.FromResult<Stream?>(null);

        return Task.FromResult<Stream?>(File.OpenRead(filePath));
    }

    private string GetContactProfileImageDirectory(Guid contactId)
    {
        return Path.Combine(_basePath, "contacts", contactId.ToString());
    }

    #endregion
}
