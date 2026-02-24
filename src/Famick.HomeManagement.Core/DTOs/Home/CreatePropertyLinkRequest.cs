namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Request to create a property link
/// </summary>
public class CreatePropertyLinkRequest
{
    /// <summary>
    /// The URL of the link
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// User-defined label (e.g., "County Records", "Zillow")
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Display order (optional, defaults to end of list)
    /// </summary>
    public int? SortOrder { get; set; }
}
