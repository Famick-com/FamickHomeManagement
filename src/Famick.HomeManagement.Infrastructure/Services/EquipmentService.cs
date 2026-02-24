using Famick.HomeManagement.Core.DTOs.Equipment;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class EquipmentService : IEquipmentService
{
    private readonly HomeManagementDbContext _context;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly ILogger<EquipmentService> _logger;

    private static readonly string[] DefaultDocumentTags = new[]
    {
        "Manual",
        "Receipt",
        "Warranty Card",
        "Installation Guide",
        "Service Record",
        "Specification Sheet"
    };

    public EquipmentService(
        HomeManagementDbContext context,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        ILogger<EquipmentService> logger)
    {
        _context = context;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _logger = logger;
    }

    #region Equipment CRUD

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating equipment: {Name}", request.Name);

        var equipment = new Equipment
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Icon = request.Icon,
            Description = request.Description,
            Location = request.Location,
            ModelNumber = request.ModelNumber,
            SerialNumber = request.SerialNumber,
            Manufacturer = request.Manufacturer,
            ManufacturerLink = request.ManufacturerLink,
            UsageUnit = request.UsageUnit,
            PurchaseDate = ToUtcDate(request.PurchaseDate),
            PurchaseLocation = request.PurchaseLocation,
            WarrantyExpirationDate = ToUtcDate(request.WarrantyExpirationDate),
            WarrantyContactInfo = request.WarrantyContactInfo,
            Notes = request.Notes,
            CategoryId = request.CategoryId,
            ParentEquipmentId = request.ParentEquipmentId
        };

        _context.Equipment.Add(equipment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Equipment created: {Id}", equipment.Id);

        return await GetByIdAsync(equipment.Id, ct) ?? throw new InvalidOperationException("Failed to retrieve created equipment");
    }

    public async Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var equipment = await _context.Equipment
            .Include(e => e.Category)
            .Include(e => e.ParentEquipment)
            .Include(e => e.Documents)
                .ThenInclude(d => d.Tag)
            .Include(e => e.ChildEquipment)
            .Include(e => e.Chores)
            .Include(e => e.UsageLogs.OrderByDescending(l => l.Date))
            .Include(e => e.MaintenanceRecords.OrderByDescending(r => r.CompletedDate))
                .ThenInclude(r => r.ReminderChore)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (equipment == null) return null;

        return MapToDto(equipment, includeDocuments: true, includeChildren: true);
    }

    public async Task<List<EquipmentSummaryDto>> ListAsync(EquipmentFilterRequest? filter = null, CancellationToken ct = default)
    {
        var query = _context.Equipment
            .Include(e => e.Category)
            .Include(e => e.ChildEquipment)
            .AsQueryable();

        // Apply filters
        if (filter != null)
        {
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.ToLower();
                query = query.Where(e =>
                    e.Name.ToLower().Contains(term) ||
                    (e.Location != null && e.Location.ToLower().Contains(term)) ||
                    (e.ModelNumber != null && e.ModelNumber.ToLower().Contains(term)) ||
                    (e.SerialNumber != null && e.SerialNumber.ToLower().Contains(term)));
            }

            if (filter.CategoryId.HasValue)
            {
                query = query.Where(e => e.CategoryId == filter.CategoryId);
            }

            if (!filter.IncludeAllLevels)
            {
                // Filter to specific parent level (null = root level)
                query = query.Where(e => e.ParentEquipmentId == filter.ParentEquipmentId);
            }

            if (filter.WarrantyExpired == true)
            {
                query = query.Where(e => e.WarrantyExpirationDate.HasValue && e.WarrantyExpirationDate < DateTime.UtcNow);
            }

            if (filter.WarrantyExpiringSoon == true)
            {
                var soonDate = DateTime.UtcNow.AddDays(30);
                query = query.Where(e =>
                    e.WarrantyExpirationDate.HasValue &&
                    e.WarrantyExpirationDate >= DateTime.UtcNow &&
                    e.WarrantyExpirationDate <= soonDate);
            }

            // Apply sorting
            query = filter.SortBy?.ToLower() switch
            {
                "name" => filter.Descending ? query.OrderByDescending(e => e.Name) : query.OrderBy(e => e.Name),
                "location" => filter.Descending ? query.OrderByDescending(e => e.Location) : query.OrderBy(e => e.Location),
                "category" => filter.Descending ? query.OrderByDescending(e => e.Category!.Name) : query.OrderBy(e => e.Category!.Name),
                "warrantyexpirationdate" => filter.Descending ? query.OrderByDescending(e => e.WarrantyExpirationDate) : query.OrderBy(e => e.WarrantyExpirationDate),
                _ => query.OrderBy(e => e.Name)
            };
        }
        else
        {
            // Default: show root level only, sorted by name
            query = query.Where(e => e.ParentEquipmentId == null).OrderBy(e => e.Name);
        }

        var equipment = await query.ToListAsync(ct);

        return equipment.Select(MapToSummaryDto).ToList();
    }

    public async Task<EquipmentDto> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating equipment: {Id}", id);

        var equipment = await _context.Equipment.FindAsync(new object[] { id }, ct);
        if (equipment == null)
        {
            throw new EntityNotFoundException(nameof(Equipment), id);
        }

        equipment.Name = request.Name;
        equipment.Icon = request.Icon;
        equipment.Description = request.Description;
        equipment.Location = request.Location;
        equipment.ModelNumber = request.ModelNumber;
        equipment.SerialNumber = request.SerialNumber;
        equipment.Manufacturer = request.Manufacturer;
        equipment.ManufacturerLink = request.ManufacturerLink;
        equipment.UsageUnit = request.UsageUnit;
        equipment.PurchaseDate = ToUtcDate(request.PurchaseDate);
        equipment.PurchaseLocation = request.PurchaseLocation;
        equipment.WarrantyExpirationDate = ToUtcDate(request.WarrantyExpirationDate);
        equipment.WarrantyContactInfo = request.WarrantyContactInfo;
        equipment.Notes = request.Notes;
        equipment.CategoryId = request.CategoryId;
        equipment.ParentEquipmentId = request.ParentEquipmentId;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Equipment updated: {Id}", id);

        return await GetByIdAsync(id, ct) ?? throw new InvalidOperationException("Failed to retrieve updated equipment");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting equipment: {Id}", id);

        var equipment = await _context.Equipment
            .Include(e => e.Documents)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (equipment == null)
        {
            throw new EntityNotFoundException(nameof(Equipment), id);
        }

        // Delete associated files
        await _fileStorage.DeleteAllEquipmentDocumentsAsync(id, ct);

        _context.Equipment.Remove(equipment);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Equipment deleted: {Id}", id);
    }

    #endregion

    #region Hierarchical Queries

    public async Task<List<EquipmentTreeDto>> GetEquipmentTreeAsync(CancellationToken ct = default)
    {
        var allEquipment = await _context.Equipment
            .Include(e => e.Category)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        // Build tree structure
        var rootItems = allEquipment.Where(e => e.ParentEquipmentId == null).ToList();
        return rootItems.Select(e => BuildTreeNode(e, allEquipment)).ToList();
    }

    public async Task<List<EquipmentSummaryDto>> GetChildEquipmentAsync(Guid parentId, CancellationToken ct = default)
    {
        var children = await _context.Equipment
            .Include(e => e.Category)
            .Include(e => e.ChildEquipment)
            .Where(e => e.ParentEquipmentId == parentId)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);

        return children.Select(MapToSummaryDto).ToList();
    }

    private EquipmentTreeDto BuildTreeNode(Equipment equipment, List<Equipment> allEquipment)
    {
        var children = allEquipment.Where(e => e.ParentEquipmentId == equipment.Id).ToList();

        return new EquipmentTreeDto
        {
            Id = equipment.Id,
            Name = equipment.Name,
            Icon = equipment.Icon,
            Location = equipment.Location,
            CategoryId = equipment.CategoryId,
            CategoryName = equipment.Category?.Name,
            WarrantyExpirationDate = equipment.WarrantyExpirationDate,
            Children = children.Select(c => BuildTreeNode(c, allEquipment)).ToList()
        };
    }

    #endregion

    #region Category Management

    public async Task<EquipmentCategoryDto> CreateCategoryAsync(CreateEquipmentCategoryRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating equipment category: {Name}", request.Name);

        // Check for duplicate name
        var exists = await _context.EquipmentCategories
            .AnyAsync(c => c.Name.ToLower() == request.Name.ToLower(), ct);

        if (exists)
        {
            throw new DuplicateEntityException(nameof(EquipmentCategory), "Name", request.Name);
        }

        var category = new EquipmentCategory
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IconName = request.IconName,
            SortOrder = request.SortOrder
        };

        _context.EquipmentCategories.Add(category);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Equipment category created: {Id}", category.Id);

        return MapToCategoryDto(category, 0);
    }

    public async Task<List<EquipmentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        var categories = await _context.EquipmentCategories
            .Include(c => c.Equipment)
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        return categories.Select(c => MapToCategoryDto(c, c.Equipment?.Count ?? 0)).ToList();
    }

    public async Task<EquipmentCategoryDto> UpdateCategoryAsync(Guid id, UpdateEquipmentCategoryRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating equipment category: {Id}", id);

        var category = await _context.EquipmentCategories
            .Include(c => c.Equipment)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (category == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentCategory), id);
        }

        // Check for duplicate name (excluding self)
        var duplicate = await _context.EquipmentCategories
            .AnyAsync(c => c.Id != id && c.Name.ToLower() == request.Name.ToLower(), ct);

        if (duplicate)
        {
            throw new DuplicateEntityException(nameof(EquipmentCategory), "Name", request.Name);
        }

        category.Name = request.Name;
        category.Description = request.Description;
        category.IconName = request.IconName;
        category.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Equipment category updated: {Id}", id);

        return MapToCategoryDto(category, category.Equipment?.Count ?? 0);
    }

    public async Task DeleteCategoryAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting equipment category: {Id}", id);

        var category = await _context.EquipmentCategories.FindAsync(new object[] { id }, ct);
        if (category == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentCategory), id);
        }

        _context.EquipmentCategories.Remove(category);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Equipment category deleted: {Id}", id);
    }

    #endregion

    #region Document Management

    public async Task<EquipmentDocumentDto> AddDocumentAsync(
        Guid equipmentId,
        Stream fileStream,
        string fileName,
        string contentType,
        AddEquipmentDocumentRequest? request = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Adding document to equipment {EquipmentId}: {FileName}", equipmentId, fileName);

        var equipment = await _context.Equipment.FindAsync(new object[] { equipmentId }, ct);
        if (equipment == null)
        {
            throw new EntityNotFoundException(nameof(Equipment), equipmentId);
        }

        // Save file to storage
        var storedFileName = await _fileStorage.SaveEquipmentDocumentAsync(equipmentId, fileStream, fileName, ct);

        // Get next sort order
        var maxSortOrder = await _context.EquipmentDocuments
            .Where(d => d.EquipmentId == equipmentId)
            .Select(d => (int?)d.SortOrder)
            .MaxAsync(ct) ?? -1;

        var document = new EquipmentDocument
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            FileName = storedFileName,
            OriginalFileName = fileName,
            ContentType = contentType,
            FileSize = fileStream.Length,
            DisplayName = request?.DisplayName,
            TagId = request?.TagId,
            SortOrder = maxSortOrder + 1
        };

        _context.EquipmentDocuments.Add(document);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Document added: {Id}", document.Id);

        return await GetDocumentDto(document, ct);
    }

    public async Task<List<EquipmentDocumentDto>> GetDocumentsAsync(Guid equipmentId, CancellationToken ct = default)
    {
        var documents = await _context.EquipmentDocuments
            .Include(d => d.Tag)
            .Where(d => d.EquipmentId == equipmentId)
            .OrderBy(d => d.SortOrder)
            .ToListAsync(ct);

        return documents.Select(d => MapToDocumentDto(d)).ToList();
    }

    public async Task<EquipmentDocumentDto?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
    {
        // Use IgnoreQueryFilters since this is used for download endpoints where
        // access is validated via token or authenticated user context
        var document = await _context.EquipmentDocuments
            .IgnoreQueryFilters()
            .Include(d => d.Tag)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        return document == null ? null : MapToDocumentDto(document);
    }

    public async Task<EquipmentDocumentDto> UpdateDocumentAsync(Guid documentId, UpdateEquipmentDocumentRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating document: {DocumentId}", documentId);

        var document = await _context.EquipmentDocuments
            .Include(d => d.Tag)
            .FirstOrDefaultAsync(d => d.Id == documentId, ct);

        if (document == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentDocument), documentId);
        }

        document.DisplayName = request.DisplayName;
        document.TagId = request.TagId;
        document.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Document updated: {Id}", documentId);

        return await GetDocumentDto(document, ct);
    }

    public async Task DeleteDocumentAsync(Guid documentId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting document: {DocumentId}", documentId);

        var document = await _context.EquipmentDocuments.FindAsync(new object[] { documentId }, ct);
        if (document == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentDocument), documentId);
        }

        // Delete file from storage
        await _fileStorage.DeleteEquipmentDocumentAsync(document.EquipmentId, document.FileName, ct);

        _context.EquipmentDocuments.Remove(document);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Document deleted: {Id}", documentId);
    }

    public async Task ReorderDocumentsAsync(Guid equipmentId, List<Guid> orderedDocumentIds, CancellationToken ct = default)
    {
        _logger.LogInformation("Reordering documents for equipment: {EquipmentId}", equipmentId);

        var documents = await _context.EquipmentDocuments
            .Where(d => d.EquipmentId == equipmentId)
            .ToListAsync(ct);

        for (var i = 0; i < orderedDocumentIds.Count; i++)
        {
            var doc = documents.FirstOrDefault(d => d.Id == orderedDocumentIds[i]);
            if (doc != null)
            {
                doc.SortOrder = i;
            }
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Documents reordered for equipment: {EquipmentId}", equipmentId);
    }

    private async Task<EquipmentDocumentDto> GetDocumentDto(EquipmentDocument document, CancellationToken ct)
    {
        // Reload with tag if needed
        if (document.TagId.HasValue && document.Tag == null)
        {
            await _context.Entry(document).Reference(d => d.Tag).LoadAsync(ct);
        }

        return MapToDocumentDto(document);
    }

    #endregion

    #region Document Tag Management

    public async Task<EquipmentDocumentTagDto> CreateTagAsync(CreateEquipmentDocumentTagRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating document tag: {Name}", request.Name);

        // Check for duplicate name
        var exists = await _context.EquipmentDocumentTags
            .AnyAsync(t => t.Name.ToLower() == request.Name.ToLower(), ct);

        if (exists)
        {
            throw new DuplicateEntityException(nameof(EquipmentDocumentTag), "Name", request.Name);
        }

        var tag = new EquipmentDocumentTag
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            IsDefault = false,
            SortOrder = request.SortOrder
        };

        _context.EquipmentDocumentTags.Add(tag);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Document tag created: {Id}", tag.Id);

        return MapToTagDto(tag, 0);
    }

    public async Task<List<EquipmentDocumentTagDto>> ListTagsAsync(CancellationToken ct = default)
    {
        var tags = await _context.EquipmentDocumentTags
            .Include(t => t.Documents)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

        return tags.Select(t => MapToTagDto(t, t.Documents?.Count ?? 0)).ToList();
    }

    public async Task<EquipmentDocumentTagDto> UpdateTagAsync(Guid id, UpdateEquipmentDocumentTagRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating document tag: {Id}", id);

        var tag = await _context.EquipmentDocumentTags
            .Include(t => t.Documents)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tag == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentDocumentTag), id);
        }

        // Check for duplicate name (excluding self)
        var duplicate = await _context.EquipmentDocumentTags
            .AnyAsync(t => t.Id != id && t.Name.ToLower() == request.Name.ToLower(), ct);

        if (duplicate)
        {
            throw new DuplicateEntityException(nameof(EquipmentDocumentTag), "Name", request.Name);
        }

        tag.Name = request.Name;
        tag.SortOrder = request.SortOrder;

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Document tag updated: {Id}", id);

        return MapToTagDto(tag, tag.Documents?.Count ?? 0);
    }

    public async Task DeleteTagAsync(Guid id, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting document tag: {Id}", id);

        var tag = await _context.EquipmentDocumentTags.FindAsync(new object[] { id }, ct);
        if (tag == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentDocumentTag), id);
        }

        _context.EquipmentDocumentTags.Remove(tag);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Document tag deleted: {Id}", id);
    }

    public async Task SeedDefaultTagsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Seeding default document tags");

        var existingTags = await _context.EquipmentDocumentTags
            .Select(t => t.Name.ToLower())
            .ToListAsync(ct);

        var sortOrder = existingTags.Count;
        foreach (var tagName in DefaultDocumentTags)
        {
            if (!existingTags.Contains(tagName.ToLower()))
            {
                _context.EquipmentDocumentTags.Add(new EquipmentDocumentTag
                {
                    Id = Guid.NewGuid(),
                    Name = tagName,
                    IsDefault = true,
                    SortOrder = sortOrder++
                });

                _logger.LogInformation("Added default tag: {Name}", tagName);
            }
        }

        await _context.SaveChangesAsync(ct);
    }

    #endregion

    #region Usage Tracking

    public async Task<EquipmentUsageLogDto> AddUsageLogAsync(Guid equipmentId, CreateEquipmentUsageLogRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding usage log to equipment: {EquipmentId}", equipmentId);

        var equipment = await _context.Equipment.FindAsync(new object[] { equipmentId }, ct);
        if (equipment == null)
        {
            throw new EntityNotFoundException(nameof(Equipment), equipmentId);
        }

        var log = new EquipmentUsageLog
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            Date = ToUtcDate(request.Date) ?? DateTime.UtcNow,
            Reading = request.Reading,
            Notes = request.Notes
        };

        _context.EquipmentUsageLogs.Add(log);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Usage log added: {Id}", log.Id);

        return MapToUsageLogDto(log);
    }

    public async Task<List<EquipmentUsageLogDto>> GetUsageLogsAsync(Guid equipmentId, CancellationToken ct = default)
    {
        var logs = await _context.EquipmentUsageLogs
            .Where(l => l.EquipmentId == equipmentId)
            .OrderByDescending(l => l.Date)
            .ToListAsync(ct);

        return logs.Select(MapToUsageLogDto).ToList();
    }

    public async Task DeleteUsageLogAsync(Guid logId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting usage log: {LogId}", logId);

        var log = await _context.EquipmentUsageLogs.FindAsync(new object[] { logId }, ct);
        if (log == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentUsageLog), logId);
        }

        _context.EquipmentUsageLogs.Remove(log);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Usage log deleted: {Id}", logId);
    }

    #endregion

    #region Maintenance Records

    public async Task<EquipmentMaintenanceRecordDto> AddMaintenanceRecordAsync(Guid equipmentId, CreateEquipmentMaintenanceRecordRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Adding maintenance record to equipment: {EquipmentId}", equipmentId);

        var equipment = await _context.Equipment.FindAsync(new object[] { equipmentId }, ct);
        if (equipment == null)
        {
            throw new EntityNotFoundException(nameof(Equipment), equipmentId);
        }

        Guid? reminderChoreId = null;

        // Create reminder chore if requested
        if (request.CreateReminder && request.ReminderDueDate.HasValue)
        {
            var chore = new Chore
            {
                Id = Guid.NewGuid(),
                Name = request.ReminderName ?? request.Description,
                Description = $"Reminder for {request.Description} maintenance",
                PeriodType = "manually",
                EquipmentId = equipmentId
            };

            _context.Chores.Add(chore);
            reminderChoreId = chore.Id;

            _logger.LogInformation("Created reminder chore: {ChoreId}", chore.Id);
        }

        var record = new EquipmentMaintenanceRecord
        {
            Id = Guid.NewGuid(),
            EquipmentId = equipmentId,
            Description = request.Description,
            CompletedDate = ToUtcDate(request.CompletedDate) ?? DateTime.UtcNow,
            UsageAtCompletion = request.UsageAtCompletion,
            Notes = request.Notes,
            ReminderChoreId = reminderChoreId
        };

        _context.EquipmentMaintenanceRecords.Add(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Maintenance record added: {Id}", record.Id);

        // Reload with chore to get the name
        if (reminderChoreId.HasValue)
        {
            await _context.Entry(record).Reference(r => r.ReminderChore).LoadAsync(ct);
        }

        return MapToMaintenanceRecordDto(record);
    }

    public async Task<List<EquipmentMaintenanceRecordDto>> GetMaintenanceRecordsAsync(Guid equipmentId, CancellationToken ct = default)
    {
        var records = await _context.EquipmentMaintenanceRecords
            .Include(r => r.ReminderChore)
            .Where(r => r.EquipmentId == equipmentId)
            .OrderByDescending(r => r.CompletedDate)
            .ToListAsync(ct);

        return records.Select(MapToMaintenanceRecordDto).ToList();
    }

    public async Task DeleteMaintenanceRecordAsync(Guid recordId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting maintenance record: {RecordId}", recordId);

        var record = await _context.EquipmentMaintenanceRecords.FindAsync(new object[] { recordId }, ct);
        if (record == null)
        {
            throw new EntityNotFoundException(nameof(EquipmentMaintenanceRecord), recordId);
        }

        _context.EquipmentMaintenanceRecords.Remove(record);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Maintenance record deleted: {Id}", recordId);
    }

    #endregion

    #region Mapping Helpers

    private EquipmentDto MapToDto(Equipment equipment, bool includeDocuments = false, bool includeChildren = false)
    {
        var latestUsageLog = equipment.UsageLogs?.OrderByDescending(l => l.Date).FirstOrDefault();

        var dto = new EquipmentDto
        {
            Id = equipment.Id,
            Name = equipment.Name,
            Icon = equipment.Icon,
            Description = equipment.Description,
            Location = equipment.Location,
            ModelNumber = equipment.ModelNumber,
            SerialNumber = equipment.SerialNumber,
            Manufacturer = equipment.Manufacturer,
            ManufacturerLink = equipment.ManufacturerLink,
            UsageUnit = equipment.UsageUnit,
            PurchaseDate = equipment.PurchaseDate,
            PurchaseLocation = equipment.PurchaseLocation,
            WarrantyExpirationDate = equipment.WarrantyExpirationDate,
            WarrantyContactInfo = equipment.WarrantyContactInfo,
            Notes = equipment.Notes,
            CategoryId = equipment.CategoryId,
            CategoryName = equipment.Category?.Name,
            ParentEquipmentId = equipment.ParentEquipmentId,
            ParentEquipmentName = equipment.ParentEquipment?.Name,
            ChildEquipmentCount = equipment.ChildEquipment?.Count ?? 0,
            DocumentCount = equipment.Documents?.Count ?? 0,
            RelatedChoreCount = equipment.Chores?.Count ?? 0,
            MaintenanceRecordCount = equipment.MaintenanceRecords?.Count ?? 0,
            LatestUsageReading = latestUsageLog?.Reading,
            LatestUsageDate = latestUsageLog?.Date,
            CreatedAt = equipment.CreatedAt,
            UpdatedAt = equipment.UpdatedAt
        };

        if (includeDocuments && equipment.Documents != null)
        {
            dto.Documents = equipment.Documents
                .OrderBy(d => d.SortOrder)
                .Select(MapToDocumentDto)
                .ToList();
        }

        if (includeChildren && equipment.ChildEquipment != null)
        {
            dto.ChildEquipment = equipment.ChildEquipment
                .OrderBy(e => e.Name)
                .Select(MapToSummaryDto)
                .ToList();
        }

        // Include usage logs if available
        if (equipment.UsageLogs != null)
        {
            dto.UsageLogs = equipment.UsageLogs
                .OrderByDescending(l => l.Date)
                .Select(MapToUsageLogDto)
                .ToList();
        }

        // Include maintenance records if available
        if (equipment.MaintenanceRecords != null)
        {
            dto.MaintenanceRecords = equipment.MaintenanceRecords
                .OrderByDescending(r => r.CompletedDate)
                .Select(MapToMaintenanceRecordDto)
                .ToList();
        }

        return dto;
    }

    private EquipmentSummaryDto MapToSummaryDto(Equipment equipment)
    {
        return new EquipmentSummaryDto
        {
            Id = equipment.Id,
            Name = equipment.Name,
            Icon = equipment.Icon,
            Location = equipment.Location,
            CategoryId = equipment.CategoryId,
            CategoryName = equipment.Category?.Name,
            WarrantyExpirationDate = equipment.WarrantyExpirationDate,
            HasParent = equipment.ParentEquipmentId.HasValue,
            ChildCount = equipment.ChildEquipment?.Count ?? 0
        };
    }

    private EquipmentDocumentDto MapToDocumentDto(EquipmentDocument document)
    {
        // Generate a signed access token for browser-initiated requests (img src, iframe, etc.)
        var accessToken = _tokenService.GenerateToken(
            "equipment-document",
            document.Id,
            document.TenantId);

        return new EquipmentDocumentDto
        {
            Id = document.Id,
            EquipmentId = document.EquipmentId,
            TenantId = document.TenantId,
            FileName = document.FileName,
            OriginalFileName = document.OriginalFileName,
            ContentType = document.ContentType,
            FileSize = document.FileSize,
            DisplayName = document.DisplayName,
            SortOrder = document.SortOrder,
            TagId = document.TagId,
            TagName = document.Tag?.Name,
            Url = _fileStorage.GetEquipmentDocumentUrl(document.Id, accessToken),
            CreatedAt = document.CreatedAt,
            UpdatedAt = document.UpdatedAt
        };
    }

    private static EquipmentCategoryDto MapToCategoryDto(EquipmentCategory category, int equipmentCount)
    {
        return new EquipmentCategoryDto
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            IconName = category.IconName,
            SortOrder = category.SortOrder,
            EquipmentCount = equipmentCount,
            CreatedAt = category.CreatedAt,
            UpdatedAt = category.UpdatedAt
        };
    }

    private static EquipmentDocumentTagDto MapToTagDto(EquipmentDocumentTag tag, int documentCount)
    {
        return new EquipmentDocumentTagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            IsDefault = tag.IsDefault,
            SortOrder = tag.SortOrder,
            DocumentCount = documentCount,
            CreatedAt = tag.CreatedAt,
            UpdatedAt = tag.UpdatedAt
        };
    }

    private static EquipmentUsageLogDto MapToUsageLogDto(EquipmentUsageLog log)
    {
        return new EquipmentUsageLogDto
        {
            Id = log.Id,
            EquipmentId = log.EquipmentId,
            Date = log.Date,
            Reading = log.Reading,
            Notes = log.Notes,
            CreatedAt = log.CreatedAt
        };
    }

    private static EquipmentMaintenanceRecordDto MapToMaintenanceRecordDto(EquipmentMaintenanceRecord record)
    {
        return new EquipmentMaintenanceRecordDto
        {
            Id = record.Id,
            EquipmentId = record.EquipmentId,
            Description = record.Description,
            CompletedDate = record.CompletedDate,
            UsageAtCompletion = record.UsageAtCompletion,
            Notes = record.Notes,
            ReminderChoreId = record.ReminderChoreId,
            ReminderChoreName = record.ReminderChore?.Name,
            CreatedAt = record.CreatedAt
        };
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Converts a DateTime to UTC. For dates with Unspecified kind (from date pickers),
    /// specifies them as UTC without changing the value.
    /// </summary>
    private static DateTime? ToUtcDate(DateTime? date)
    {
        if (!date.HasValue) return null;

        return date.Value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(date.Value, DateTimeKind.Utc)
            : date.Value.ToUniversalTime();
    }

    #endregion
}
