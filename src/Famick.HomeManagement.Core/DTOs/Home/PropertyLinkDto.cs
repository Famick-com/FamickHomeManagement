namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Property link data transfer object
/// </summary>
public class PropertyLinkDto
{
    public Guid Id { get; set; }
    public Guid HomeId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
