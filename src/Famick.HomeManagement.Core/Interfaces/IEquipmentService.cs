using Famick.HomeManagement.Core.DTOs.Equipment;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing household equipment
/// </summary>
public interface IEquipmentService
{
    #region Equipment CRUD

    /// <summary>
    /// Creates a new equipment item
    /// </summary>
    Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets equipment by ID with full details
    /// </summary>
    Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lists equipment with optional filtering
    /// </summary>
    Task<List<EquipmentSummaryDto>> ListAsync(EquipmentFilterRequest? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing equipment item
    /// </summary>
    Task<EquipmentDto> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes an equipment item and its documents
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    #endregion

    #region Hierarchical Queries

    /// <summary>
    /// Gets equipment as a hierarchical tree (root items with nested children)
    /// </summary>
    Task<List<EquipmentTreeDto>> GetEquipmentTreeAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets child equipment for a specific parent
    /// </summary>
    Task<List<EquipmentSummaryDto>> GetChildEquipmentAsync(Guid parentId, CancellationToken ct = default);

    #endregion

    #region Category Management

    /// <summary>
    /// Creates a new equipment category
    /// </summary>
    Task<EquipmentCategoryDto> CreateCategoryAsync(CreateEquipmentCategoryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lists all equipment categories
    /// </summary>
    Task<List<EquipmentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates an existing category
    /// </summary>
    Task<EquipmentCategoryDto> UpdateCategoryAsync(Guid id, UpdateEquipmentCategoryRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a category (sets CategoryId to null on associated equipment)
    /// </summary>
    Task DeleteCategoryAsync(Guid id, CancellationToken ct = default);

    #endregion

    #region Document Management

    /// <summary>
    /// Adds a document to equipment
    /// </summary>
    Task<EquipmentDocumentDto> AddDocumentAsync(
        Guid equipmentId,
        Stream fileStream,
        string fileName,
        string contentType,
        AddEquipmentDocumentRequest? request = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all documents for equipment
    /// </summary>
    Task<List<EquipmentDocumentDto>> GetDocumentsAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>
    /// Gets a single document by ID (for download endpoint).
    /// Uses IgnoreQueryFilters - caller must validate tenant access.
    /// </summary>
    Task<EquipmentDocumentDto?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// Updates document metadata
    /// </summary>
    Task<EquipmentDocumentDto> UpdateDocumentAsync(Guid documentId, UpdateEquipmentDocumentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a document
    /// </summary>
    Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>
    /// Reorders documents for equipment
    /// </summary>
    Task ReorderDocumentsAsync(Guid equipmentId, List<Guid> orderedDocumentIds, CancellationToken ct = default);

    #endregion

    #region Document Tag Management

    /// <summary>
    /// Creates a new document tag
    /// </summary>
    Task<EquipmentDocumentTagDto> CreateTagAsync(CreateEquipmentDocumentTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Lists all document tags
    /// </summary>
    Task<List<EquipmentDocumentTagDto>> ListTagsAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates an existing tag
    /// </summary>
    Task<EquipmentDocumentTagDto> UpdateTagAsync(Guid id, UpdateEquipmentDocumentTagRequest request, CancellationToken ct = default);

    /// <summary>
    /// Deletes a tag (sets TagId to null on associated documents)
    /// </summary>
    Task DeleteTagAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Seeds default document tags if they don't exist
    /// </summary>
    Task SeedDefaultTagsAsync(CancellationToken ct = default);

    #endregion

    #region Usage Tracking

    /// <summary>
    /// Adds a usage log entry to equipment
    /// </summary>
    Task<EquipmentUsageLogDto> AddUsageLogAsync(Guid equipmentId, CreateEquipmentUsageLogRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets all usage logs for equipment
    /// </summary>
    Task<List<EquipmentUsageLogDto>> GetUsageLogsAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a usage log entry
    /// </summary>
    Task DeleteUsageLogAsync(Guid logId, CancellationToken ct = default);

    #endregion

    #region Maintenance Records

    /// <summary>
    /// Adds a maintenance record to equipment
    /// </summary>
    Task<EquipmentMaintenanceRecordDto> AddMaintenanceRecordAsync(Guid equipmentId, CreateEquipmentMaintenanceRecordRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets all maintenance records for equipment
    /// </summary>
    Task<List<EquipmentMaintenanceRecordDto>> GetMaintenanceRecordsAsync(Guid equipmentId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a maintenance record
    /// </summary>
    Task DeleteMaintenanceRecordAsync(Guid recordId, CancellationToken ct = default);

    #endregion
}
