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

    #region Master Product Licensed Images

    /// <summary>
    /// Saves a licensed master product image to storage.
    /// </summary>
    Task<string> SaveMasterProductImageAsync(Guid masterProductId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes a licensed master product image from storage.
    /// </summary>
    Task DeleteMasterProductImageAsync(Guid masterProductId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Gets the URL for accessing a licensed master product image via the secure API endpoint.
    /// </summary>
    string GetMasterProductImageUrl(Guid masterProductId, Guid imageId, string? accessToken = null);

    /// <summary>
    /// Gets the physical file path for a licensed master product image.
    /// </summary>
    string GetMasterProductImagePath(Guid masterProductId, string fileName);

    /// <summary>
    /// Gets a readable stream for a licensed master product image.
    /// </summary>
    Task<Stream?> GetMasterProductImageStreamAsync(Guid masterProductId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes all licensed images for a master product.
    /// </summary>
    Task DeleteAllMasterProductImagesAsync(Guid masterProductId, CancellationToken ct = default);

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

    #region Export Archives

    /// <summary>
    /// Stores a completed export archive.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Takes a stream rather than a path because the archive is built to a temp file and handed
    /// over without being loaded into memory — a household's archive can run to gigabytes.
    /// </para>
    /// <para>
    /// <strong>The caller owns the stream.</strong> An implementation must read it and leave it
    /// open. This is stated because it was once true of one implementation and not the other:
    /// the local one copied and left it alone while the S3 one closed it underneath the caller,
    /// so anything the caller did with the stream afterwards failed only in the cloud.
    /// </para>
    /// </remarks>
    /// <param name="transferId">The export session this archive belongs to.</param>
    /// <param name="stream">The archive content.</param>
    /// <param name="fileName">The download file name, e.g. famick-export-2026-09-06.zip.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stored file name.</returns>
    Task<string> SaveExportArchiveAsync(Guid transferId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Size and last-modified time of a stored archive, or null when it is not there.
    /// </summary>
    /// <remarks>
    /// Needed separately from the stream so a range request can answer with a total length
    /// without opening the object.
    /// </remarks>
    Task<StoredFileInfo?> GetExportArchiveInfoAsync(Guid transferId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Opens a stored archive for reading, optionally a byte range of it.
    /// </summary>
    /// <remarks>
    /// The range is explicit rather than left to the response layer. Object-storage streams are
    /// not seekable, so a FileStreamResult cannot work out a length and silently serves the whole
    /// object — which is the wrong behaviour on the largest file this product will ever hand out,
    /// and breaks clients that fetch large files in chunks.
    /// </remarks>
    /// <param name="rangeStart">First byte to return, or null for the beginning.</param>
    /// <param name="rangeEnd">Last byte to return, inclusive, or null for the end.</param>
    Task<Stream?> GetExportArchiveStreamAsync(Guid transferId, string fileName, long? rangeStart = null, long? rangeEnd = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a stored archive. Used when it expires, and when the user asks.
    /// </summary>
    Task DeleteExportArchiveAsync(Guid transferId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// The download URL for an archive, through the authenticated API endpoint.
    /// </summary>
    string GetExportArchiveUrl(Guid transferId, string? accessToken = null);

    #endregion

    #region Restore Uploads

    /// <summary>
    /// Stores an uploaded archive awaiting a restore.
    /// </summary>
    Task<string> SaveRestoreUploadAsync(Guid transferId, Stream stream, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Opens an uploaded archive for reading. Read twice: once to classify, once to apply.
    /// </summary>
    Task<Stream?> GetRestoreUploadStreamAsync(Guid transferId, string fileName, CancellationToken ct = default);

    /// <summary>
    /// Deletes an uploaded archive, once the restore finishes or is abandoned.
    /// </summary>
    Task DeleteRestoreUploadAsync(Guid transferId, string fileName, CancellationToken ct = default);

    #endregion
}
