namespace Famick.HomeManagement.Core.DTOs.Equipment;

/// <summary>
/// Equipment document data transfer object
/// </summary>
public class EquipmentDocumentDto
{
    public Guid Id { get; set; }
    public Guid EquipmentId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? DisplayName { get; set; }
    public int SortOrder { get; set; }
    public Guid? TagId { get; set; }
    public string? TagName { get; set; }

    /// <summary>
    /// URL to access the document
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly display name (uses DisplayName if set, otherwise OriginalFileName)
    /// </summary>
    public string DisplayLabel => !string.IsNullOrEmpty(DisplayName) ? DisplayName : OriginalFileName;

    /// <summary>
    /// Human-readable file size
    /// </summary>
    public string FormattedFileSize => FormatFileSize(FileSize);

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
