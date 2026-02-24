using Famick.HomeManagement.Core.DTOs.StorageBins;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class StorageBinService : IStorageBinService
{
    private readonly HomeManagementDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly ILogger<StorageBinService> _logger;

    public StorageBinService(
        HomeManagementDbContext context,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        ILogger<StorageBinService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _logger = logger;
    }

    #region Storage Bin CRUD

    public async Task<StorageBinDto> CreateAsync(CreateStorageBinRequest request, CancellationToken ct = default)
    {
        var shortCode = await GenerateUniqueShortCodeAsync(ct);

        _logger.LogInformation("Creating storage bin: {ShortCode}", shortCode);

        var storageBin = new StorageBin
        {
            Id = Guid.NewGuid(),
            ShortCode = shortCode,
            Description = request.Description,
            LocationId = request.LocationId,
            Category = request.Category
        };

        _context.StorageBins.Add(storageBin);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Storage bin created: {Id} ({ShortCode})", storageBin.Id, shortCode);

        return await GetByIdAsync(storageBin.Id, ct) ?? throw new InvalidOperationException("Failed to retrieve created storage bin");
    }

    public async Task<List<StorageBinDto>> CreateBatchAsync(CreateStorageBinBatchRequest request, CancellationToken ct = default)
    {
        if (request.Count < 1 || request.Count > 100)
        {
            throw new ArgumentException("Count must be between 1 and 100", nameof(request));
        }

        _logger.LogInformation("Creating {Count} storage bins", request.Count);

        var bins = new List<StorageBin>();
        var usedCodes = new HashSet<string>();

        for (var i = 0; i < request.Count; i++)
        {
            string shortCode;
            do
            {
                shortCode = ShortCodeGenerator.Generate();
            } while (usedCodes.Contains(shortCode) || await ShortCodeExistsAsync(shortCode, ct));

            usedCodes.Add(shortCode);

            bins.Add(new StorageBin
            {
                Id = Guid.NewGuid(),
                ShortCode = shortCode,
                Description = string.Empty
            });
        }

        _context.StorageBins.AddRange(bins);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created {Count} storage bins", bins.Count);

        return bins.Select(b => MapToDto(b, includePhotos: false)).ToList();
    }

    public async Task<StorageBinDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var bin = await _context.StorageBins
            .Include(b => b.Photos)
            .Include(b => b.Location)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (bin == null) return null;

        return MapToDto(bin, includePhotos: true);
    }

    public async Task<StorageBinDto?> GetByShortCodeAsync(string shortCode, CancellationToken ct = default)
    {
        var bin = await _context.StorageBins
            .Include(b => b.Photos)
            .Include(b => b.Location)
            .FirstOrDefaultAsync(b => b.ShortCode == shortCode, ct);

        if (bin == null) return null;

        return MapToDto(bin, includePhotos: true);
    }

    public async Task<List<StorageBinSummaryDto>> ListAsync(CancellationToken ct = default)
    {
        var bins = await _context.StorageBins
            .Include(b => b.Photos)
            .Include(b => b.Location)
            .OrderBy(b => b.ShortCode)
            .ToListAsync(ct);

        return bins.Select(MapToSummaryDto).ToList();
    }

    public async Task<StorageBinDto> UpdateAsync(Guid id, UpdateStorageBinRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating storage bin: {Id}", id);

        var bin = await _context.StorageBins.FindAsync(new object[] { id }, ct);
        if (bin == null)
        {
            throw new EntityNotFoundException(nameof(StorageBin), id);
        }

        bin.Description = request.Description;
        bin.LocationId = request.LocationId;
        bin.Category = request.Category;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Storage bin updated: {Id}", id);

        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve updated storage bin");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting storage bin: {Id}", id);

        var bin = await _context.StorageBins
            .Include(b => b.Photos)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (bin == null)
        {
            throw new EntityNotFoundException(nameof(StorageBin), id);
        }

        // Delete associated files
        await _fileStorage.DeleteAllStorageBinPhotosAsync(id, ct);

        _context.StorageBins.Remove(bin);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Storage bin deleted: {Id}", id);
    }

    #endregion

    #region Photo Management

    public async Task<StorageBinPhotoDto> AddPhotoAsync(
        Guid storageBinId,
        Stream fileStream,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Adding photo to storage bin {StorageBinId}: {FileName}", storageBinId, fileName);

        var bin = await _context.StorageBins.FindAsync(new object[] { storageBinId }, ct);
        if (bin == null)
        {
            throw new EntityNotFoundException(nameof(StorageBin), storageBinId);
        }

        // Save file to storage
        var storedFileName = await _fileStorage.SaveStorageBinPhotoAsync(storageBinId, fileStream, fileName, contentType, ct);

        // Get next sort order
        var maxSortOrder = await _context.StorageBinPhotos
            .Where(p => p.StorageBinId == storageBinId)
            .Select(p => (int?)p.SortOrder)
            .MaxAsync(ct) ?? -1;

        var photo = new StorageBinPhoto
        {
            Id = Guid.NewGuid(),
            StorageBinId = storageBinId,
            FileName = storedFileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileStream.Length,
            SortOrder = maxSortOrder + 1
        };

        _context.StorageBinPhotos.Add(photo);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Photo added: {Id}", photo.Id);

        return MapToPhotoDto(photo);
    }

    public async Task<List<StorageBinPhotoDto>> GetPhotosAsync(Guid storageBinId, CancellationToken ct = default)
    {
        var photos = await _context.StorageBinPhotos
            .Where(p => p.StorageBinId == storageBinId)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return photos.Select(MapToPhotoDto).ToList();
    }

    public async Task<StorageBinPhotoDto?> GetPhotoByIdAsync(Guid photoId, CancellationToken ct = default)
    {
        var photo = await _context.StorageBinPhotos
            .FirstOrDefaultAsync(p => p.Id == photoId, ct);

        return photo == null ? null : MapToPhotoDto(photo);
    }

    public async Task DeletePhotoAsync(Guid photoId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting photo: {PhotoId}", photoId);

        var photo = await _context.StorageBinPhotos.FindAsync(new object[] { photoId }, ct);
        if (photo == null)
        {
            throw new EntityNotFoundException(nameof(StorageBinPhoto), photoId);
        }

        // Delete file from storage
        await _fileStorage.DeleteStorageBinPhotoAsync(photo.StorageBinId, photo.FileName, ct);

        _context.StorageBinPhotos.Remove(photo);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Photo deleted: {Id}", photoId);
    }

    public async Task ReorderPhotosAsync(Guid storageBinId, List<Guid> orderedPhotoIds, CancellationToken ct = default)
    {
        _logger.LogInformation("Reordering photos for storage bin: {StorageBinId}", storageBinId);

        var photos = await _context.StorageBinPhotos
            .Where(p => p.StorageBinId == storageBinId)
            .ToListAsync(ct);

        for (var i = 0; i < orderedPhotoIds.Count; i++)
        {
            var photo = photos.FirstOrDefault(p => p.Id == orderedPhotoIds[i]);
            if (photo != null)
            {
                photo.SortOrder = i;
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Photos reordered for storage bin: {StorageBinId}", storageBinId);
    }

    #endregion

    #region Short Code

    public async Task<string> GenerateUniqueShortCodeAsync(CancellationToken ct = default)
    {
        const int maxAttempts = 100;
        var attempts = 0;

        while (attempts < maxAttempts)
        {
            var shortCode = ShortCodeGenerator.Generate();

            if (!await ShortCodeExistsAsync(shortCode, ct))
            {
                return shortCode;
            }

            attempts++;
        }

        throw new InvalidOperationException("Failed to generate unique short code after maximum attempts");
    }

    private async Task<bool> ShortCodeExistsAsync(string shortCode, CancellationToken ct)
    {
        return await _context.StorageBins.AnyAsync(b => b.ShortCode == shortCode, ct);
    }

    #endregion

    #region Mapping Helpers

    private StorageBinDto MapToDto(StorageBin bin, bool includePhotos = false)
    {
        var dto = new StorageBinDto
        {
            Id = bin.Id,
            ShortCode = bin.ShortCode,
            Description = bin.Description,
            LocationId = bin.LocationId,
            LocationName = bin.Location?.Name,
            Category = bin.Category,
            PhotoCount = bin.Photos?.Count ?? 0,
            CreatedAt = bin.CreatedAt,
            UpdatedAt = bin.UpdatedAt
        };

        if (includePhotos && bin.Photos != null)
        {
            dto.Photos = bin.Photos
                .OrderBy(p => p.SortOrder)
                .Select(MapToPhotoDto)
                .ToList();
        }

        return dto;
    }

    private StorageBinSummaryDto MapToSummaryDto(StorageBin bin)
    {
        return new StorageBinSummaryDto
        {
            Id = bin.Id,
            ShortCode = bin.ShortCode,
            DescriptionPreview = GetFirstLine(bin.Description),
            LocationId = bin.LocationId,
            LocationName = bin.Location?.Name,
            Category = bin.Category,
            PhotoCount = bin.Photos?.Count ?? 0,
            CreatedAt = bin.CreatedAt
        };
    }

    private StorageBinPhotoDto MapToPhotoDto(StorageBinPhoto photo)
    {
        // Generate a signed access token for browser-initiated requests
        var accessToken = _tokenService.GenerateToken(
            "storage-bin-photo",
            photo.Id,
            photo.TenantId);

        return new StorageBinPhotoDto
        {
            Id = photo.Id,
            StorageBinId = photo.StorageBinId,
            FileName = photo.FileName,
            OriginalFileName = photo.OriginalFileName,
            ContentType = photo.ContentType,
            FileSize = photo.FileSize,
            SortOrder = photo.SortOrder,
            Url = _fileStorage.GetStorageBinPhotoUrl(photo.Id, accessToken),
            CreatedAt = photo.CreatedAt,
            UpdatedAt = photo.UpdatedAt
        };
    }

    private static string GetFirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Skip leading whitespace and empty lines
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Skip markdown headers (#) but include their content
            if (trimmed.StartsWith('#'))
            {
                var content = trimmed.TrimStart('#').Trim();
                if (!string.IsNullOrEmpty(content))
                    return content;
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                return trimmed;
            }
        }
        return string.Empty;
    }

    #endregion
}
