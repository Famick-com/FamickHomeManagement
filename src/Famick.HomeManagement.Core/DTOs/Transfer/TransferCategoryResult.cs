namespace Famick.HomeManagement.Core.DTOs.Transfer;

/// <summary>
/// Final result for a single category after transfer completes.
/// </summary>
public class TransferCategoryResult
{
    /// <summary>
    /// Category name (e.g., "Locations", "Products")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>
    /// Per-item results for this category
    /// </summary>
    public List<TransferItemResult> Items { get; set; } = new();
}

/// <summary>
/// Result of transferring a single item
/// </summary>
public class TransferItemResult
{
    public string Name { get; set; } = string.Empty;
    public TransferItemStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}
