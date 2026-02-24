namespace Famick.HomeManagement.Core.DTOs.Transfer;

/// <summary>
/// Real-time progress snapshot returned by the polling endpoint during transfer.
/// </summary>
public class TransferProgress
{
    /// <summary>
    /// Overall session status
    /// </summary>
    public TransferSessionStatus SessionStatus { get; set; }

    /// <summary>
    /// Name of the category currently being transferred (e.g., "Products")
    /// </summary>
    public string CurrentCategory { get; set; } = string.Empty;

    /// <summary>
    /// Zero-based index of the current category in the overall transfer order
    /// </summary>
    public int CurrentCategoryIndex { get; set; }

    /// <summary>
    /// Total number of categories to transfer
    /// </summary>
    public int TotalCategories { get; set; }

    /// <summary>
    /// Zero-based index of the current item within the current category
    /// </summary>
    public int CurrentItemIndex { get; set; }

    /// <summary>
    /// Total items in the current category
    /// </summary>
    public int TotalItemsInCategory { get; set; }

    /// <summary>
    /// Display name of the item currently being transferred
    /// </summary>
    public string? CurrentItemName { get; set; }

    /// <summary>
    /// Status of the last transferred item
    /// </summary>
    public TransferItemStatus? LastItemStatus { get; set; }

    /// <summary>
    /// Count of items created in the current category so far
    /// </summary>
    public int CategoryCreatedCount { get; set; }

    /// <summary>
    /// Count of items skipped (duplicates) in the current category so far
    /// </summary>
    public int CategorySkippedCount { get; set; }

    /// <summary>
    /// Count of items that failed in the current category so far
    /// </summary>
    public int CategoryFailedCount { get; set; }

    /// <summary>
    /// Overall progress as a percentage (0-100)
    /// </summary>
    public double OverallProgressPercent { get; set; }

    /// <summary>
    /// Summary of already-completed categories with their final counts
    /// </summary>
    public List<TransferCategorySummary> CompletedCategories { get; set; } = new();
}

/// <summary>
/// Summary of a completed category during transfer
/// </summary>
public class TransferCategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
}

/// <summary>
/// Status of an overall transfer session
/// </summary>
public enum TransferSessionStatus
{
    InProgress,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Status of an individual transferred item
/// </summary>
public enum TransferItemStatus
{
    Created,
    Skipped,
    Failed
}
