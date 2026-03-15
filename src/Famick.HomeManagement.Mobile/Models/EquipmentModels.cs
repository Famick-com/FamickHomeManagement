namespace Famick.HomeManagement.Mobile.Models;

public class EquipmentSummaryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? CategoryName { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsWarrantyExpired { get; set; }
    public bool WarrantyExpiringSoon { get; set; }
    public int? DaysUntilWarrantyExpires { get; set; }
    public Guid? ParentEquipmentId { get; set; }
    public int ChildCount { get; set; }
    public int DocumentCount { get; set; }

    // Tree display properties (set client-side)
    public bool IsExpanded { get; set; }
    public int IndentLevel { get; set; }
    public bool IsChildItem => ParentEquipmentId.HasValue;

    public string SubtitleDisplay
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Location)) parts.Add(Location);
            if (!string.IsNullOrEmpty(CategoryName)) parts.Add(CategoryName);
            return parts.Count > 0 ? string.Join(" | ", parts) : string.Empty;
        }
    }

    public Color WarrantyColor =>
        IsWarrantyExpired ? Color.FromArgb("#D32F2F")
        : WarrantyExpiringSoon ? Color.FromArgb("#F57C00")
        : Colors.Transparent;

    public bool HasWarrantyStatus => IsWarrantyExpired || WarrantyExpiringSoon;
    public bool HasChildren => ChildCount > 0;
    public string ExpandIcon => IsExpanded ? "▼" : "▶";
    public int IndentWidth => IndentLevel * 24;

    public string WarrantyStatusText =>
        IsWarrantyExpired ? "Warranty Expired"
        : WarrantyExpiringSoon ? $"Warranty expires in {DaysUntilWarrantyExpires}d"
        : string.Empty;
}

public class EquipmentDetailItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerLink { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public DateTime? WarrantyExpirationDate { get; set; }
    public string? WarrantyContactInfo { get; set; }
    public string? UsageUnit { get; set; }
    public string? Notes { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid? ParentEquipmentId { get; set; }
    public string? ParentEquipmentName { get; set; }
    public bool IsWarrantyExpired { get; set; }
    public int? DaysUntilWarrantyExpires { get; set; }
    public int DocumentCount { get; set; }
    public int MaintenanceRecordCount { get; set; }
    public int UsageLogCount { get; set; }
    public int ChildCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool WarrantyExpiringSoon =>
        DaysUntilWarrantyExpires.HasValue && DaysUntilWarrantyExpires.Value is > 0 and <= 30;
}

public class EquipmentCategoryItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int SortOrder { get; set; }
    public int EquipmentCount { get; set; }
}

public class EquipmentDocumentItem
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? FormattedFileSize { get; set; }
    public string? TagName { get; set; }
    public Guid? TagId { get; set; }
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }

    public string DisplayLabel => !string.IsNullOrEmpty(DisplayName) ? DisplayName : OriginalFileName;
}

public class EquipmentUsageLogItem
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public DateTime Date { get; set; }
    public decimal Reading { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public string DateDisplay => Date.ToLocalTime().ToString("MMM d, yyyy");
}

public class EquipmentMaintenanceRecordItem
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public decimal? UsageAtCompletion { get; set; }
    public string? Notes { get; set; }
    public string? ReminderChoreName { get; set; }
    public DateTime CreatedAt { get; set; }

    public string DateDisplay => CompletedDate.ToLocalTime().ToString("MMM d, yyyy");
}

public class CreateEquipmentMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerLink { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public DateTime? WarrantyExpirationDate { get; set; }
    public string? WarrantyContactInfo { get; set; }
    public string? UsageUnit { get; set; }
    public string? Notes { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ParentEquipmentId { get; set; }
}

public class UpdateEquipmentMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? ModelNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Manufacturer { get; set; }
    public string? ManufacturerLink { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? PurchaseLocation { get; set; }
    public DateTime? WarrantyExpirationDate { get; set; }
    public string? WarrantyContactInfo { get; set; }
    public string? UsageUnit { get; set; }
    public string? Notes { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? ParentEquipmentId { get; set; }
}

public class CreateEquipmentCategoryMobileRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

public class CreateEquipmentUsageLogMobileRequest
{
    public DateTime Date { get; set; }
    public decimal Reading { get; set; }
    public string? Notes { get; set; }
}

public class CreateEquipmentMaintenanceRecordMobileRequest
{
    public string Description { get; set; } = string.Empty;
    public DateTime CompletedDate { get; set; }
    public decimal? UsageAtCompletion { get; set; }
    public string? Notes { get; set; }
    public bool CreateReminder { get; set; }
    public string? ReminderName { get; set; }
    public DateTime? ReminderDueDate { get; set; }
}
