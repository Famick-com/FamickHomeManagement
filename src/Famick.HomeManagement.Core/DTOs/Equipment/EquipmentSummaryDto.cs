namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Lightweight equipment summary for lists and dropdowns
/// </summary>
public class EquipmentSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Location { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime? WarrantyExpirationDate { get; set; }

    /// <summary>
    /// Whether the warranty has expired
    /// </summary>
    public bool IsWarrantyExpired => WarrantyExpirationDate.HasValue && WarrantyExpirationDate.Value < DateTime.UtcNow;

    /// <summary>
    /// Whether the warranty expires within 30 days
    /// </summary>
    public bool IsWarrantyExpiringSoon => WarrantyExpirationDate.HasValue
        && !IsWarrantyExpired
        && (WarrantyExpirationDate.Value - DateTime.UtcNow).TotalDays <= 30;

    /// <summary>
    /// Whether this equipment has a parent
    /// </summary>
    public bool HasParent { get; set; }

    /// <summary>
    /// Number of child equipment items
    /// </summary>
    public int ChildCount { get; set; }
}
