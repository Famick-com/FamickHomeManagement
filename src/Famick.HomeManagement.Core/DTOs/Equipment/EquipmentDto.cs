namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Full equipment data transfer object with all details
/// </summary>
public class EquipmentDto
{
    public Guid Id { get; set; }

    #region Basic Information

    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }

    #endregion

    #region Identification

    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerLink { get; set; }
    public string? UsageUnit { get; set; }

    #endregion

    #region Purchase Information

    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }

    #endregion

    #region Warranty Information

    public DateTime? WarrantyExpirationDate { get; set; }
    public string? WarrantyContactInfo { get; set; }

    /// <summary>
    /// Whether the warranty has expired
    /// </summary>
    public bool IsWarrantyExpired => WarrantyExpirationDate.HasValue && WarrantyExpirationDate.Value < DateTime.UtcNow;

    /// <summary>
    /// Days until warranty expires (negative if expired)
    /// </summary>
    public int? DaysUntilWarrantyExpires => WarrantyExpirationDate.HasValue
        ? (int)(WarrantyExpirationDate.Value - DateTime.UtcNow).TotalDays
        : null;

    #endregion

    #region Notes

    public string? Notes { get; set; }

    #endregion

    #region Category

    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }

    #endregion

    #region Parent Equipment

    public Guid? ParentEquipmentId { get; set; }
    public string? ParentEquipmentName { get; set; }

    #endregion

    #region Related Data Counts

    public int ChildEquipmentCount { get; set; }
    public int DocumentCount { get; set; }
    public int RelatedChoreCount { get; set; }
    public int MaintenanceRecordCount { get; set; }

    #endregion

    #region Usage & Maintenance

    public decimal? LatestUsageReading { get; set; }
    public DateTime? LatestUsageDate { get; set; }
    public List<EquipmentUsageLogDto>? UsageLogs { get; set; }
    public List<EquipmentMaintenanceRecordDto>? MaintenanceRecords { get; set; }

    #endregion

    #region Documents (optional full load)

    public List<EquipmentDocumentDto>? Documents { get; set; }

    #endregion

    #region Child Equipment (optional full load)

    public List<EquipmentSummaryDto>? ChildEquipment { get; set; }

    #endregion

    #region Audit

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    #endregion
}
