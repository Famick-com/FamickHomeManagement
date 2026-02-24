using Famick.HomeManagement.Core.DTOs.Equipment;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Famick.HomeManagement.Web.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing household equipment, categories, documents, and tags
/// </summary>
[ApiController]
[Route("api/v1/equipment")]
[Authorize]
public class EquipmentController : ApiControllerBase
{
    private readonly IEquipmentService _equipmentService;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;

    private static readonly string[] AllowedDocumentTypes = new[]
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain"
    };

    private const long MaxDocumentSize = 20 * 1024 * 1024; // 20MB

    public EquipmentController(
        IEquipmentService equipmentService,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        ITenantProvider tenantProvider,
        ILogger<EquipmentController> logger)
        : base(tenantProvider, logger)
    {
        _equipmentService = equipmentService;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
    }

    #region Equipment CRUD

    /// <summary>
    /// Gets a list of equipment with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<EquipmentSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> List(
        [FromQuery] EquipmentFilterRequest? filter,
        CancellationToken ct)
    {
        _logger.LogInformation("Listing equipment for tenant {TenantId}", TenantId);

        var equipment = await _equipmentService.ListAsync(filter, ct);

        return ApiResponse(equipment);
    }

    /// <summary>
    /// Gets equipment as a hierarchical tree
    /// </summary>
    [HttpGet("tree")]
    [ProducesResponseType(typeof(List<EquipmentTreeDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetTree(CancellationToken ct)
    {
        _logger.LogInformation("Getting equipment tree for tenant {TenantId}", TenantId);

        var tree = await _equipmentService.GetEquipmentTreeAsync(ct);

        return ApiResponse(tree);
    }

    /// <summary>
    /// Gets a single equipment item by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(EquipmentDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting equipment {EquipmentId} for tenant {TenantId}", id, TenantId);

        var equipment = await _equipmentService.GetByIdAsync(id, ct);

        if (equipment == null)
        {
            return NotFoundResponse("Equipment not found");
        }

        return ApiResponse(equipment);
    }

    /// <summary>
    /// Creates a new equipment item
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEquipmentRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } }
            });
        }

        _logger.LogInformation("Creating equipment '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var equipment = await _equipmentService.CreateAsync(request, ct);

        return CreatedAtAction(nameof(Get), new { id = equipment.Id }, equipment);
    }

    /// <summary>
    /// Updates an existing equipment item
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEquipmentRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } }
            });
        }

        _logger.LogInformation("Updating equipment {EquipmentId} for tenant {TenantId}", id, TenantId);

        var equipment = await _equipmentService.UpdateAsync(id, request, ct);

        return ApiResponse(equipment);
    }

    /// <summary>
    /// Deletes an equipment item
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting equipment {EquipmentId} for tenant {TenantId}", id, TenantId);

        await _equipmentService.DeleteAsync(id, ct);

        return NoContent();
    }

    /// <summary>
    /// Gets child equipment for a parent
    /// </summary>
    [HttpGet("{id}/children")]
    [ProducesResponseType(typeof(List<EquipmentSummaryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetChildren(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting child equipment for {ParentId} in tenant {TenantId}", id, TenantId);

        var children = await _equipmentService.GetChildEquipmentAsync(id, ct);

        return ApiResponse(children);
    }

    #endregion

    #region Categories

    /// <summary>
    /// Gets all equipment categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<EquipmentCategoryDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ListCategories(CancellationToken ct)
    {
        _logger.LogInformation("Listing equipment categories for tenant {TenantId}", TenantId);

        var categories = await _equipmentService.ListCategoriesAsync(ct);

        return ApiResponse(categories);
    }

    /// <summary>
    /// Creates a new equipment category
    /// </summary>
    [HttpPost("categories")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentCategoryDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateEquipmentCategoryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } }
            });
        }

        _logger.LogInformation("Creating equipment category '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var category = await _equipmentService.CreateCategoryAsync(request, ct);

        return CreatedAtAction(nameof(ListCategories), category);
    }

    /// <summary>
    /// Updates an existing equipment category
    /// </summary>
    [HttpPut("categories/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentCategoryDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateCategory(
        Guid id,
        [FromBody] UpdateEquipmentCategoryRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } }
            });
        }

        _logger.LogInformation("Updating equipment category {CategoryId} for tenant {TenantId}", id, TenantId);

        var category = await _equipmentService.UpdateCategoryAsync(id, request, ct);

        return ApiResponse(category);
    }

    /// <summary>
    /// Deletes an equipment category
    /// </summary>
    [HttpDelete("categories/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteCategory(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting equipment category {CategoryId} for tenant {TenantId}", id, TenantId);

        await _equipmentService.DeleteCategoryAsync(id, ct);

        return NoContent();
    }

    #endregion

    #region Documents

    /// <summary>
    /// Gets all documents for an equipment item
    /// </summary>
    [HttpGet("{id}/documents")]
    [ProducesResponseType(typeof(List<EquipmentDocumentDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDocuments(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting documents for equipment {EquipmentId} in tenant {TenantId}", id, TenantId);

        var documents = await _equipmentService.GetDocumentsAsync(id, ct);

        return ApiResponse(documents);
    }

    /// <summary>
    /// Downloads an equipment document (secure file access with tenant validation).
    /// Accepts either Authorization header OR a valid access token in query string.
    /// </summary>
    [HttpGet("documents/{documentId}/download")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadDocument(Guid documentId, [FromQuery] string? token, CancellationToken ct)
    {
        _logger.LogInformation("Downloading document {DocumentId}", documentId);

        // Validate access and get expected tenant ID
        var expectedTenantId = ValidateFileAccess(_tokenService, token, "equipment-document", documentId);
        if (!expectedTenantId.HasValue)
        {
            return Unauthorized();
        }

        // Load document (uses IgnoreQueryFilters)
        var document = await _equipmentService.GetDocumentByIdAsync(documentId, ct);
        if (document == null)
        {
            return NotFoundResponse("Document not found");
        }

        // Validate tenant access
        if (!ValidateTenantAccess(document.TenantId, expectedTenantId.Value))
        {
            return NotFoundResponse("Document not found");
        }

        var stream = await _fileStorage.GetEquipmentDocumentStreamAsync(document.EquipmentId, document.FileName, ct);
        if (stream == null)
        {
            _logger.LogWarning("Document file not found: equipment {EquipmentId}, file {FileName}", document.EquipmentId, document.FileName);
            return NotFoundResponse("Document file not found");
        }

        // Return without filename to display inline (Content-Disposition: inline)
        // instead of triggering download (Content-Disposition: attachment)
        return File(stream, document.ContentType);
    }

    /// <summary>
    /// Uploads a document to equipment
    /// </summary>
    [HttpPost("{id}/documents")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentDocumentDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadDocument(
        Guid id,
        [FromForm] UploadEquipmentDocumentRequest uploadRequest,
        CancellationToken ct)
    {
        if (uploadRequest.File == null || uploadRequest.File.Length == 0)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "file", new[] { "File is required" } }
            });
        }

        if (uploadRequest.File.Length > MaxDocumentSize)
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "file", new[] { $"File size exceeds maximum of {MaxDocumentSize / 1024 / 1024}MB" } }
            });
        }

        if (!AllowedDocumentTypes.Contains(uploadRequest.File.ContentType.ToLowerInvariant()))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "file", new[] { "File type not allowed. Allowed types: PDF, images, Word, Excel, text" } }
            });
        }

        _logger.LogInformation("Uploading document '{FileName}' to equipment {EquipmentId} for tenant {TenantId}",
            uploadRequest.File.FileName, id, TenantId);

        await using var stream = uploadRequest.File.OpenReadStream();
        var request = new AddEquipmentDocumentRequest
        {
            DisplayName = uploadRequest.DisplayName,
            TagId = uploadRequest.TagId
        };

        var document = await _equipmentService.AddDocumentAsync(id, stream, uploadRequest.File.FileName, uploadRequest.File.ContentType, request, ct);

        return CreatedAtAction(nameof(GetDocuments), new { id }, document);
    }

    /// <summary>
    /// Updates document metadata
    /// </summary>
    [HttpPut("documents/{documentId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentDocumentDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateDocument(
        Guid documentId,
        [FromBody] UpdateEquipmentDocumentRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Updating document {DocumentId} for tenant {TenantId}", documentId, TenantId);

        var document = await _equipmentService.UpdateDocumentAsync(documentId, request, ct);

        return ApiResponse(document);
    }

    /// <summary>
    /// Deletes a document
    /// </summary>
    [HttpDelete("documents/{documentId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteDocument(Guid documentId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting document {DocumentId} for tenant {TenantId}", documentId, TenantId);

        await _equipmentService.DeleteDocumentAsync(documentId, ct);

        return NoContent();
    }

    /// <summary>
    /// Reorders documents for an equipment item
    /// </summary>
    [HttpPut("{id}/documents/reorder")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ReorderDocuments(
        Guid id,
        [FromBody] List<Guid> orderedDocumentIds,
        CancellationToken ct)
    {
        _logger.LogInformation("Reordering documents for equipment {EquipmentId} in tenant {TenantId}", id, TenantId);

        await _equipmentService.ReorderDocumentsAsync(id, orderedDocumentIds, ct);

        return NoContent();
    }

    #endregion

    #region Document Tags

    /// <summary>
    /// Gets all document tags
    /// </summary>
    [HttpGet("document-tags")]
    [ProducesResponseType(typeof(List<EquipmentDocumentTagDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> ListTags(CancellationToken ct)
    {
        _logger.LogInformation("Listing document tags for tenant {TenantId}", TenantId);

        var tags = await _equipmentService.ListTagsAsync(ct);

        return ApiResponse(tags);
    }

    /// <summary>
    /// Creates a new document tag
    /// </summary>
    [HttpPost("document-tags")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentDocumentTagDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> CreateTag(
        [FromBody] CreateEquipmentDocumentTagRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } }
            });
        }

        _logger.LogInformation("Creating document tag '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var tag = await _equipmentService.CreateTagAsync(request, ct);

        return CreatedAtAction(nameof(ListTags), tag);
    }

    /// <summary>
    /// Updates an existing document tag
    /// </summary>
    [HttpPut("document-tags/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentDocumentTagDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> UpdateTag(
        Guid id,
        [FromBody] UpdateEquipmentDocumentTagRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Name is required" } }
            });
        }

        _logger.LogInformation("Updating document tag {TagId} for tenant {TenantId}", id, TenantId);

        var tag = await _equipmentService.UpdateTagAsync(id, request, ct);

        return ApiResponse(tag);
    }

    /// <summary>
    /// Deletes a document tag
    /// </summary>
    [HttpDelete("document-tags/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteTag(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Deleting document tag {TagId} for tenant {TenantId}", id, TenantId);

        await _equipmentService.DeleteTagAsync(id, ct);

        return NoContent();
    }

    #endregion

    #region Usage Tracking

    /// <summary>
    /// Gets usage logs for an equipment item
    /// </summary>
    [HttpGet("{id}/usage")]
    [ProducesResponseType(typeof(List<EquipmentUsageLogDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetUsageLogs(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting usage logs for equipment {EquipmentId} in tenant {TenantId}", id, TenantId);

        var logs = await _equipmentService.GetUsageLogsAsync(id, ct);

        return ApiResponse(logs);
    }

    /// <summary>
    /// Adds a usage log entry to equipment
    /// </summary>
    [HttpPost("{id}/usage")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentUsageLogDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddUsageLog(
        Guid id,
        [FromBody] CreateEquipmentUsageLogRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation("Adding usage log to equipment {EquipmentId} for tenant {TenantId}", id, TenantId);

        var log = await _equipmentService.AddUsageLogAsync(id, request, ct);

        return CreatedAtAction(nameof(GetUsageLogs), new { id }, log);
    }

    /// <summary>
    /// Deletes a usage log entry
    /// </summary>
    [HttpDelete("usage/{logId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteUsageLog(Guid logId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting usage log {LogId} for tenant {TenantId}", logId, TenantId);

        await _equipmentService.DeleteUsageLogAsync(logId, ct);

        return NoContent();
    }

    #endregion

    #region Maintenance Records

    /// <summary>
    /// Gets maintenance records for an equipment item
    /// </summary>
    [HttpGet("{id}/maintenance")]
    [ProducesResponseType(typeof(List<EquipmentMaintenanceRecordDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetMaintenanceRecords(Guid id, CancellationToken ct)
    {
        _logger.LogInformation("Getting maintenance records for equipment {EquipmentId} in tenant {TenantId}", id, TenantId);

        var records = await _equipmentService.GetMaintenanceRecordsAsync(id, ct);

        return ApiResponse(records);
    }

    /// <summary>
    /// Adds a maintenance record to equipment
    /// </summary>
    [HttpPost("{id}/maintenance")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(EquipmentMaintenanceRecordDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> AddMaintenanceRecord(
        Guid id,
        [FromBody] CreateEquipmentMaintenanceRecordRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Description", new[] { "Description is required" } }
            });
        }

        _logger.LogInformation("Adding maintenance record to equipment {EquipmentId} for tenant {TenantId}", id, TenantId);

        var record = await _equipmentService.AddMaintenanceRecordAsync(id, request, ct);

        return CreatedAtAction(nameof(GetMaintenanceRecords), new { id }, record);
    }

    /// <summary>
    /// Deletes a maintenance record
    /// </summary>
    [HttpDelete("maintenance/{recordId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteMaintenanceRecord(Guid recordId, CancellationToken ct)
    {
        _logger.LogInformation("Deleting maintenance record {RecordId} for tenant {TenantId}", recordId, TenantId);

        await _equipmentService.DeleteMaintenanceRecordAsync(recordId, ct);

        return NoContent();
    }

    #endregion
}
