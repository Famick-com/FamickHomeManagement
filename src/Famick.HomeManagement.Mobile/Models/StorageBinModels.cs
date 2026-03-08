namespace Famick.HomeManagement.Mobile.Models;

public class StorageBinSummaryItem
{
    public Guid Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string? DescriptionPreview { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? Category { get; set; }
    public int PhotoCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string SubtitleDisplay
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(LocationName)) parts.Add(LocationName);
            if (!string.IsNullOrEmpty(Category)) parts.Add(Category);
            return parts.Count > 0 ? string.Join(" | ", parts) : string.Empty;
        }
    }
}

public class StorageBinDetailItem
{
    public Guid Id { get; set; }
    public string ShortCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public string? Category { get; set; }
    public int PhotoCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<StorageBinPhotoItem> Photos { get; set; } = new();
}

public class StorageBinPhotoItem
{
    public Guid Id { get; set; }
    public Guid StorageBinId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }
    public string? Url { get; set; }
    public string? FormattedFileSize { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateStorageBinMobileRequest
{
    public string? Description { get; set; }
    public Guid? LocationId { get; set; }
    public string? Category { get; set; }
}

public class UpdateStorageBinMobileRequest
{
    public string? Description { get; set; }
    public Guid? LocationId { get; set; }
    public string? Category { get; set; }
}

public class CreateStorageBinBatchMobileRequest
{
    public int Count { get; set; }
}

public class GenerateLabelSheetMobileRequest
{
    public int SheetCount { get; set; } = 1;
    public List<Guid>? BinIds { get; set; }
    public int LabelFormat { get; set; }
    public bool RepeatToFill { get; set; }
}

public record StorageBinLabelPopupResult(
    int SheetCount,
    int LabelFormat,
    bool RepeatToFill,
    List<Guid>? BinIds);

public class StorageBinGroup : List<StorageBinSummaryItem>
{
    public string Name { get; }

    public StorageBinGroup(string name, IEnumerable<StorageBinSummaryItem> items) : base(items)
    {
        Name = name;
    }
}
