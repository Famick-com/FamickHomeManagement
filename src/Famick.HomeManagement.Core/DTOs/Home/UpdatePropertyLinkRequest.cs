namespace Famick.HomeManagement.Core.DTOs.Home;

/// <summary>
/// Request to update a property link
/// </summary>
public class UpdatePropertyLinkRequest
{
    /// <summary>
    /// The URL of the link
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// User-defined label
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Display order
    /// </summary>
    public int SortOrder { get; set; }
}
