using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Hierarchical equipment tree node for tree view display
/// </summary>
public class EquipmentTreeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Location { get; set; }
    public EquipmentKind Kind { get; set; }
    public bool IsActive { get; set; } = true;
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
    /// Child equipment in the hierarchy
    /// </summary>
    public List<EquipmentTreeDto> Children { get; set; } = new();
}
