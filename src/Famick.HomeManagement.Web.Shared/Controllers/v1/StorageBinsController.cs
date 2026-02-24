using Famick.HomeManagement.Core.DTOs.StorageBins;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Famick.HomeManagement.Web.Shared.Models;
using Famick.HomeManagement.Web.Shared.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing storage bins with QR code labels
/// </summary>
[ApiController]
[Route("api/v1/storage-bins")]
[Authorize]
public class StorageBinsController : ApiControllerBase
{
    private readonly IStorageBinService _storageBinService;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly QrCodeService _qrCodeService;
    private readonly LabelSheetService _labelSheetService;

    private static readonly string[] AllowedPhotoTypes = new[]
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp"
    };

    private const long MaxPhotoSize = 10 * 1024 * 1024; // 10MB

    public StorageBinsController(
        IStorageBinService storageBinService,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        QrCodeService qrCodeService,
        LabelSheetService labelSheetService,
        ITenantProvider tenantProvider,
        ILogger<StorageBinsController> logger)
        : base(tenantProvider, logger)
    {
        _storageBinService = storageBinService;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _qrCodeService = qrCodeService;
        _labelSheetService = labelSheetService;
    }

    #region Storage Bin CRUD

    /// <summary>
    /// Gets a list of all storage bins
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StorageBinSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        _logger.LogInformation("Listing storage bins for tenant {TenantId}", TenantId);

        var bins = await _storageBinService.ListAsync(ct);

        return ApiResponse(bins);
    }

    /// <summary>
    /// Gets a single storage bin by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StorageBinDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting storage bin {StorageBinId} for tenant {TenantId}", id, TenantId);

        var bin = await _storageBinService.GetByIdAsync(id, ct);

        if (bin == null)
        {
            return NotFoundResponse("Storage bin not found");
        }

        return ApiResponse(bin);
    }

    /// <summary>
    /// Gets a single storage bin by short code (for QR code scanning)
    /// </summary>
    [HttpGet("code/{shortCode}")]
    [ProducesResponseType(typeof(StorageBinDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetByShortCode(string shortCode, CancellationToken ct)
    {
        _logger.LogInformation("Getting storage bin by short code {ShortCode} for tenant {TenantId}", shortCode, TenantId);

        var bin = await _storageBinService.GetByShortCodeAsync(shortCode, ct);

        if (bin == null)
        {
            return NotFoundResponse("Storage bin not found");
        }

        return ApiResponse(bin);
    }

    /// <summary>
    /// Creates a new storage bin with auto-generated short code
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StorageBinDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStorageBinRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Creating storage bin for tenant {TenantId}", TenantId);

        var bin = await _storageBinService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(GetById), new { id = bin.Id }, bin);
    }

    /// <summary>
    /// Creates multiple storage bins at once (for label printing)
    /// </summary>
    [HttpPost("batch")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(List<StorageBinDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateBatch(
        [FromBody] CreateStorageBinBatchRequest request,
        CancellationToken ct)
    {
        if (request.Count < 1 || request.Count > 100)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Count", new[] { "Count must be between 1 and 100" } }
            });
        }

        _logger.LogInformation("Creating {Count} storage bins for tenant {TenantId}", request.Count, TenantId);

        var bins = await _storageBinService.CreateBatchAsync(request, ct);

        return CreatedAtAction(nameof(List), bins);
    }

    /// <summary>
    /// Updates an existing storage bin
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StorageBinDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateStorageBinRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating storage bin {StorageBinId} for tenant {TenantId}", id, TenantId);

        var bin = await _storageBinService.UpdateAsync(id, request, ct);

        return ApiResponse(bin);
    }

    /// <summary>
    /// Deletes a storage bin
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting storage bin {StorageBinId} for tenant {TenantId}", id, TenantId);

        await _storageBinService.DeleteAsync(id, ct);

        return NoContent();
    }

    #endregion

    #region Photos

    /// <summary>
    /// Gets all photos for a storage bin
    /// </summary>
    [HttpGet("{id:guid}/photos")]
    [ProducesResponseType(typeof(List<StorageBinPhotoDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPhotos(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting photos for storage bin {StorageBinId} in tenant {TenantId}", id, TenantId);

        var photos = await _storageBinService.GetPhotosAsync(id, ct);

        return ApiResponse(photos);
    }

    /// <summary>
    /// Downloads a storage bin photo (secure file access with tenant validation).
    /// Accepts either Authorization header OR a valid access token in query string.
    /// </summary>
    [HttpGet("photos/{photoId:guid}/download")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadPhoto(Guid photoId, [FromQuery] string? token, CancellationToken ct)
    {
        _logger.LogInformation("Downloading photo {PhotoId}", photoId);

        // First, get the photo to validate it exists and get tenant info
        var photo = await _storageBinService.GetPhotoByIdAsync(photoId, ct);
        if (photo == null)
        {
            return NotFoundResponse("Photo not found");
        }

        // Check authorization: either authenticated user OR valid token
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var hasValidToken = !string.IsNullOrEmpty(token) &&
            _tokenService.ValidateToken(token, "storage-bin-photo", photoId, TenantId);

        if (!isAuthenticated && !hasValidToken)
        {
            return Unauthorized();
        }

        var stream = await _fileStorage.GetStorageBinPhotoStreamAsync(photo.StorageBinId, photo.FileName, ct);
        if (stream == null)
        {
            _logger.LogWarning("Photo file not found: bin {StorageBinId}, file {FileName}", photo.StorageBinId, photo.FileName);
            return NotFoundResponse("Photo file not found");
        }

        // Return without filename to display inline
        return File(stream, photo.ContentType);
    }

    /// <summary>
    /// Uploads a photo to a storage bin
    /// </summary>
    [HttpPost("{id:guid}/photos")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(StorageBinPhotoDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadPhoto(
        Guid id,
        [FromForm] UploadStorageBinPhotoRequest uploadRequest,
        CancellationToken ct)
    {
        if (uploadRequest.File == null || uploadRequest.File.Length == 0)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "file", new[] { "File is required" } }
            });
        }

        if (uploadRequest.File.Length > MaxPhotoSize)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "file", new[] { $"File size exceeds maximum of {MaxPhotoSize / 1024 / 1024}MB" } }
            });
        }

        if (!AllowedPhotoTypes.Contains(uploadRequest.File.ContentType.ToLowerInvariant()))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "file", new[] { "File type not allowed. Allowed types: JPEG, PNG, GIF, WebP" } }
            });
        }

        _logger.LogInformation("Uploading photo '{FileName}' to storage bin {StorageBinId} for tenant {TenantId}",
            uploadRequest.File.FileName, id, TenantId);

        await using var stream = uploadRequest.File.OpenReadStream();
        var photo = await _storageBinService.AddPhotoAsync(id, stream, uploadRequest.File.FileName, uploadRequest.File.ContentType, ct);

        return CreatedAtAction(nameof(GetPhotos), new { id }, photo);
    }

    /// <summary>
    /// Deletes a photo
    /// </summary>
    [HttpDelete("photos/{photoId:guid}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeletePhoto(Guid photoId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting photo {PhotoId} for tenant {TenantId}", photoId, TenantId);

        await _storageBinService.DeletePhotoAsync(photoId, ct);

        return NoContent();
    }

    /// <summary>
    /// Reorders photos for a storage bin
    /// </summary>
    [HttpPut("{id:guid}/photos/reorder")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReorderPhotos(
        Guid id,
        [FromBody] List<Guid> orderedPhotoIds,
        CancellationToken ct)
    {
        _logger.LogInformation("Reordering photos for storage bin {StorageBinId} in tenant {TenantId}", id, TenantId);

        await _storageBinService.ReorderPhotosAsync(id, orderedPhotoIds, ct);

        return NoContent();
    }

    #endregion

    #region QR Code & Labels

    /// <summary>
    /// Generates a QR code PNG for a storage bin.
    /// AllowAnonymous because img tags cannot send auth headers.
    /// </summary>
    [HttpGet("{id:guid}/qr-code")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetQrCode(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Generating QR code for storage bin {StorageBinId}", id);

        var bin = await _storageBinService.GetByIdAsync(id, ct);
        if (bin == null)
        {
            return NotFoundResponse("Storage bin not found");
        }

        var qrCodeBytes = _qrCodeService.GenerateQrCode(bin.ShortCode);

        return File(qrCodeBytes, "image/png", $"qr-{bin.ShortCode}.png");
    }

    /// <summary>
    /// Generates a PDF label sheet for printing (supports multiple Avery formats)
    /// </summary>
    [HttpPost("label-sheet")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GenerateLabelSheet(
        [FromBody] GenerateLabelSheetRequest request,
        CancellationToken ct)
    {
        if (request.SheetCount < 1 || request.SheetCount > 10)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "SheetCount", new[] { "Sheet count must be between 1 and 10" } }
            });
        }

        var spec = LabelFormatSpec.GetSpec(request.LabelFormat);
        _logger.LogInformation("Generating label sheet ({SheetCount} sheets, {Format}) for tenant {TenantId}",
            request.SheetCount, request.LabelFormat, TenantId);

        var labelsNeeded = request.SheetCount * spec.LabelsPerPage;
        var labels = new List<LabelInfo>();

        // If specific bins are provided
        if (request.BinIds != null && request.BinIds.Count > 0)
        {
            // First, collect all the bins
            var existingBins = new List<LabelInfo>();
            foreach (var binId in request.BinIds)
            {
                var bin = await _storageBinService.GetByIdAsync(binId, ct);
                if (bin != null)
                {
                    existingBins.Add(new LabelInfo { Id = bin.Id, ShortCode = bin.ShortCode, Category = bin.Category });
                }
            }

            if (request.RepeatToFill)
            {
                // Repeat bins to fill all sheets
                while (labels.Count < labelsNeeded && existingBins.Count > 0)
                {
                    foreach (var bin in existingBins)
                    {
                        if (labels.Count >= labelsNeeded) break;
                        labels.Add(new LabelInfo { Id = bin.Id, ShortCode = bin.ShortCode, Category = bin.Category });
                    }
                }
            }
            else
            {
                // Print each bin once
                labels.AddRange(existingBins);
            }
        }
        else
        {
            // No specific bins provided - create new bins
            var newBins = await _storageBinService.CreateBatchAsync(
                new CreateStorageBinBatchRequest { Count = labelsNeeded },
                ct);

            labels.AddRange(newBins.Select(b => new LabelInfo { Id = b.Id, ShortCode = b.ShortCode }));
        }

        var pdfBytes = _labelSheetService.GenerateLabelSheet(labels, request.LabelFormat);

        return File(pdfBytes, "application/pdf", $"storage-labels-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf");
    }

    #endregion
}
