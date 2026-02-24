namespace Famick.HomeManagement.Core.DTOs.Vehicles;

/// <summary>
/// Vehicle document data transfer object
/// </summary>
public class VehicleDocumentDto
{
    public Guid Id { get; set; }
    public Guid VehicleId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? DisplayName { get; set; }
    public string? DocumentType { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Indicates if the document is expired
    /// </summary>
    public bool IsExpired => ExpirationDate.HasValue && ExpirationDate.Value < DateTime.UtcNow;

    /// <summary>
    /// Indicates if the document expires soon (within 30 days)
    /// </summary>
    public bool ExpiresSoon => ExpirationDate.HasValue && !IsExpired &&
                               ExpirationDate.Value < DateTime.UtcNow.AddDays(30);
}
