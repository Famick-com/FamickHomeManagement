using Famick.HomeManagement.Core.DTOs.StorageBins;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing storage bins with QR code labels
/// </summary>
public interface IStorageBinService
{
    #region Storage Bin CRUD

    /// <summary>
    /// Creates a new storage bin with auto-generated short code
    /// </summary>
    Task<StorageBinDto> CreateAsync(CreateStorageBinRequest request, CancellationToken ct = default);

    /// <summary>
    /// Creates multiple storage bins at once (for label printing)
    /// </summary>
    Task<List<StorageBinDto>> CreateBatchAsync(CreateStorageBinBatchRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets storage bin by ID with full details
    /// </summary>
    Task<StorageBinDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Gets storage bin by short code (for QR code scanning)
    /// </summary>
    Task<StorageBinDto?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default);

    /// <summary>
    /// Lists all storage bins as summaries
    /// </summary>
    Task<List<StorageBinSummaryDto>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates an existing storage bin
    /// </summary>
    Task<StorageBinDto> UpdateAsync(Guid id, UpdateStorageBinRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a storage bin and its photos
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    #endregion

    #region Photo Management

    /// <summary>
    /// Adds a photo to a storage bin
    /// </summary>
    Task<StorageBinPhotoDto> AddPhotoAsync(
        Guid storageBinId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all photos for a storage bin
    /// </summary>
    Task<List<StorageBinPhotoDto>> GetPhotosAsync(Guid storageBinId, CancellationToken ct = default);

    /// <summary>
    /// Gets a single photo by ID (for download endpoint)
    /// </summary>
    Task<StorageBinPhotoDto?> GetPhotoByIdAsync(Guid photoId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a photo
    /// </summary>
    Task DeletePhotoAsync(Guid photoId, CancellationToken ct = default);

    /// <summary>
    /// Reorders photos for a storage bin
    /// </summary>
    Task ReorderPhotosAsync(Guid storageBinId, List<Guid> orderedPhotoIds, CancellationToken ct = default);

    #endregion

    #region Short Code

    /// <summary>
    /// Generates a unique short code that doesn't exist in the database
    /// </summary>
    Task<string> GenerateUniqueShortCodeAsync(CancellationToken ct = default);

    #endregion
}
