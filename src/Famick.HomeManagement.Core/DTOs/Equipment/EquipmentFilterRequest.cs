namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Filter and sorting options for equipment list queries
/// </summary>
public class EquipmentFilterRequest
{
    /// <summary>
    /// Search term to filter by name, location, model number, or serial number
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Filter by category ID
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Filter by parent equipment ID (null = root level only)
    /// </summary>
    public Guid? ParentEquipmentId { get; set; }

    /// <summary>
    /// Include equipment at all levels (ignores ParentEquipmentId filter)
    /// </summary>
    public bool IncludeAllLevels { get; set; }

    /// <summary>
    /// Filter to show only equipment with warranty expiring within 30 days
    /// </summary>
    public bool? WarrantyExpiringSoon { get; set; }

    /// <summary>
    /// Filter to show only equipment with expired warranty
    /// </summary>
    public bool? WarrantyExpired { get; set; }

    /// <summary>
    /// Sort by field (Name, Location, Category, WarrantyExpirationDate, CreatedAt)
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Sort in descending order
    /// </summary>
    public bool Descending { get; set; }
}
