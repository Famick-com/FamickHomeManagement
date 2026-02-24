namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for storing and retrieving files (images, documents, etc.)
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves a product image to storage.
    /// </summary>
    /// <param name="productId">The product ID for organizing storage.</param>
    /// <param name="stream">The file stream to save.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name (unique).</returns>
    Task<string> SaveProductImageAsync(Guid productId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a product image from storage.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteProductImageAsync(Guid productId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing a product image via the secure API endpoint.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="imageId">The image ID.</param>
    /// <param name="accessToken">Optional pre-signed access token for browser-initiated requests.</param>
    /// <returns>The API URL to access the image.</returns>
    string GetProductImageUrl(Guid productId, Guid imageId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for a product image.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The full file path on disk, or the S3 object key in cloud deployments.</returns>
    string GetProductImagePath(Guid productId, string fileName);

    /// <summary>
    /// Gets a readable stream for a product image.
    /// Abstracts over local file system and cloud object storage.
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream for reading the image, or null if the file does not exist.</returns>
    Task<Stream?> GetProductImageStreamAsync(Guid productId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes all images for a product (used when deleting a product).
    /// </summary>
    /// <param name="productId">The product ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAllProductImagesAsync(Guid productId, CancellationToken ct = default);

    /// <summary>
    /// Downloads an image from a URL and saves it to product storage.
    /// Used for self-hosted deployments to cache external images locally.
    /// </summary>
    /// <param name="productId">The product ID for organizing storage.</param>
    /// <param name="imageUrl">The URL to download the image from.</param>
    /// <param name="source">The source identifier (e.g., "kroger", "openfoodfacts") for folder organization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name, or null if download failed.</returns>
    Task<string?> DownloadAndSaveProductImageAsync(
        Guid productId,
        string imageUrl,
        string source,
        CancellationToken ct = default);

    #region Equipment Documents

    /// <summary>
    /// Saves an equipment document to storage.
    /// </summary>
    /// <param name="equipmentId">The equipment ID for organizing storage.</param>
    /// <param name="stream">The file stream to save.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name (unique).</returns>
    Task<string> SaveEquipmentDocumentAsync(Guid equipmentId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes an equipment document from storage.
    /// </summary>
    /// <param name="equipmentId">The equipment ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteEquipmentDocumentAsync(Guid equipmentId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing an equipment document via the secure API endpoint.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="accessToken">Optional pre-signed access token for browser-initiated requests.</param>
    /// <returns>The API URL to access the document.</returns>
    string GetEquipmentDocumentUrl(Guid documentId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for an equipment document.
    /// </summary>
    /// <param name="equipmentId">The equipment ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The full file path on disk, or the S3 object key in cloud deployments.</returns>
    string GetEquipmentDocumentPath(Guid equipmentId, string fileName);

    /// <summary>
    /// Gets a readable stream for an equipment document.
    /// Abstracts over local file system and cloud object storage.
    /// </summary>
    /// <param name="equipmentId">The equipment ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream for reading the document, or null if the file does not exist.</returns>
    Task<Stream?> GetEquipmentDocumentStreamAsync(Guid equipmentId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes all documents for an equipment item (used when deleting equipment).
    /// </summary>
    /// <param name="equipmentId">The equipment ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAllEquipmentDocumentsAsync(Guid equipmentId, CancellationToken ct = default);

    #endregion

    #region Storage Bin Photos

    /// <summary>
    /// Saves a storage bin photo to storage.
    /// </summary>
    /// <param name="storageBinId">The storage bin ID for organizing storage.</param>
    /// <param name="stream">The file stream to save.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="contentType">The MIME content type (used to determine extension if filename lacks one).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name (unique).</returns>
    Task<string> SaveStorageBinPhotoAsync(Guid storageBinId, Stream stream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Deletes a storage bin photo from storage.
    /// </summary>
    /// <param name="storageBinId">The storage bin ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteStorageBinPhotoAsync(Guid storageBinId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing a storage bin photo via the secure API endpoint.
    /// </summary>
    /// <param name="photoId">The photo ID.</param>
    /// <param name="accessToken">Optional pre-signed access token for browser-initiated requests.</param>
    /// <returns>The API URL to access the photo.</returns>
    string GetStorageBinPhotoUrl(Guid photoId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for a storage bin photo.
    /// </summary>
    /// <param name="storageBinId">The storage bin ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The full file path on disk, or the S3 object key in cloud deployments.</returns>
    string GetStorageBinPhotoPath(Guid storageBinId, string fileName);

    /// <summary>
    /// Gets a readable stream for a storage bin photo.
    /// Abstracts over local file system and cloud object storage.
    /// </summary>
    /// <param name="storageBinId">The storage bin ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream for reading the photo, or null if the file does not exist.</returns>
    Task<Stream?> GetStorageBinPhotoStreamAsync(Guid storageBinId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes all photos for a storage bin (used when deleting the bin).
    /// </summary>
    /// <param name="storageBinId">The storage bin ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAllStorageBinPhotosAsync(Guid storageBinId, CancellationToken ct = default);

    #endregion

    #region Recipe Images

    /// <summary>
    /// Saves a recipe image to storage.
    /// </summary>
    /// <param name="recipeId">The recipe ID for organizing storage.</param>
    /// <param name="stream">The file stream to save.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name (unique).</returns>
    Task<string> SaveRecipeImageAsync(Guid recipeId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a recipe image from storage.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteRecipeImageAsync(Guid recipeId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing a recipe image via the secure API endpoint.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="imageId">The image ID.</param>
    /// <param name="accessToken">Optional pre-signed access token for browser-initiated requests.</param>
    /// <returns>The API URL to access the image.</returns>
    string GetRecipeImageUrl(Guid recipeId, Guid imageId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for a recipe image.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The full file path on disk, or the S3 object key in cloud deployments.</returns>
    string GetRecipeImagePath(Guid recipeId, string fileName);

    /// <summary>
    /// Gets a readable stream for a recipe image.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream for reading the image, or null if the file does not exist.</returns>
    Task<Stream?> GetRecipeImageStreamAsync(Guid recipeId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes all images for a recipe (used when deleting a recipe).
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteAllRecipeImagesAsync(Guid recipeId, CancellationToken ct = default);

    #endregion

    #region Recipe Step Images

    /// <summary>
    /// Saves a recipe step image to storage.
    /// </summary>
    /// <param name="recipeId">The recipe ID for organizing storage.</param>
    /// <param name="stepId">The step ID for organizing storage.</param>
    /// <param name="stream">The file stream to save.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name (unique).</returns>
    Task<string> SaveRecipeStepImageAsync(Guid recipeId, Guid stepId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a recipe step image from storage.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteRecipeStepImageAsync(Guid recipeId, Guid stepId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing a recipe step image via the secure API endpoint.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="accessToken">Optional pre-signed access token for browser-initiated requests.</param>
    /// <returns>The API URL to access the image.</returns>
    string GetRecipeStepImageUrl(Guid recipeId, Guid stepId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for a recipe step image.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The full file path on disk, or the S3 object key in cloud deployments.</returns>
    string GetRecipeStepImagePath(Guid recipeId, Guid stepId, string fileName);

    /// <summary>
    /// Gets a readable stream for a recipe step image.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="stepId">The step ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream for reading the image, or null if the file does not exist.</returns>
    Task<Stream?> GetRecipeStepImageStreamAsync(Guid recipeId, Guid stepId, string fileName, CancellationToken ct = default);

    #endregion

    #region Contact Profile Images

    /// <summary>
    /// Saves a contact profile image to storage.
    /// </summary>
    /// <param name="contactId">The contact ID for organizing storage.</param>
    /// <param name="stream">The file stream to save.</param>
    /// <param name="fileName">The original file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name (unique).</returns>
    Task<string> SaveContactProfileImageAsync(Guid contactId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a contact profile image from storage.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DeleteContactProfileImageAsync(Guid contactId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing a contact profile image via the secure API endpoint.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="accessToken">Optional pre-signed access token for browser-initiated requests.</param>
    /// <returns>The API URL to access the image.</returns>
    string GetContactProfileImageUrl(Guid contactId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for a contact profile image.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <returns>The full file path on disk, or the S3 object key in cloud deployments.</returns>
    string GetContactProfileImagePath(Guid contactId, string fileName);

    /// <summary>
    /// Gets a readable stream for a contact profile image.
    /// Abstracts over local file system and cloud object storage.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="fileName">The stored file name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A stream for reading the image, or null if the file does not exist.</returns>
    Task<Stream?> GetContactProfileImageStreamAsync(Guid contactId, string fileName, CancellationToken ct = default);

    #endregion
}
