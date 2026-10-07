using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

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
    public EquipmentKind Kind { get; set; }
    public EquipmentAttributes? Attributes { get; set; }
    public bool IsActive { get; set; } = true;
    public string? PrimaryDriverName { get; set; }
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
    /// The parent equipment ID (if any)
    /// </summary>
    public Guid? ParentEquipmentId { get; set; }

    /// <summary>
    /// Whether this equipment has a parent
    /// </summary>
    public bool HasParent { get; set; }

    /// <summary>
    /// Number of child equipment items
    /// </summary>
    public int ChildCount { get; set; }
}
