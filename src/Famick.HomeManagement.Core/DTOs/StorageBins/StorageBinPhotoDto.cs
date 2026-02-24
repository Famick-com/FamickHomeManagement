namespace Famick.HomeManagement.Core.DTOs.StorageBins;

/// <summary>
/// Storage bin photo data transfer object
/// </summary>
public class StorageBinPhotoDto
{
    public Guid Id { get; set; }
    public Guid StorageBinId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int SortOrder { get; set; }

    /// <summary>
    /// URL to access the photo
    /// </summary>
    public string Url { get; set; } = string.Empty;

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
