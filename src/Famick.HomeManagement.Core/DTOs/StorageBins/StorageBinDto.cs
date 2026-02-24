namespace Famick.HomeManagement.Core.DTOs.StorageBins;

/// <summary>
/// Full storage bin data transfer object with all details
/// </summary>
public class StorageBinDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// Human-readable short code (e.g., "blue-oak-47")
    /// </summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>
    /// Markdown description of the bin contents
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// First line of description for list preview
    /// </summary>
    public string DescriptionPreview => GetFirstLine(Description);

    /// <summary>
    /// Optional location where this bin is stored
    /// </summary>
    public Guid? LocationId { get; set; }

    /// <summary>
    /// Location name for display
    /// </summary>
    public string? LocationName { get; set; }

    /// <summary>
    /// Freeform category for organizing bins
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Number of photos attached to this bin
    /// </summary>
    public int PhotoCount { get; set; }

    /// <summary>
    /// Photos attached to this bin (optional full load)
    /// </summary>
    public List<StorageBinPhotoDto>? Photos { get; set; }

    #region Audit

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    #endregion

    private static string GetFirstLine(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Skip leading whitespace and empty lines
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            // Skip markdown headers (#) but include their content
            if (trimmed.StartsWith('#'))
            {
                var content = trimmed.TrimStart('#').Trim();
                if (!string.IsNullOrEmpty(content))
                    return content;
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                return trimmed;
            }
        }
        return string.Empty;
    }
}
