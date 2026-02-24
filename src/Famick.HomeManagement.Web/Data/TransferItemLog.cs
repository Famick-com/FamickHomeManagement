using Famick.HomeManagement.Core.DTOs.Transfer;

namespace Famick.HomeManagement.Web.Data;

/// <summary>
/// Tracks the result of transferring a single item to cloud.
/// Used for progress tracking and ID mapping between local and cloud entities.
/// </summary>
public class TransferItemLog
{
    public Guid Id { get; set; }
    public Guid TransferSessionId { get; set; }

    /// <summary>
    /// Category name (e.g., "Locations", "Products")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Local entity ID
    /// </summary>
    public Guid SourceId { get; set; }

    /// <summary>
    /// Cloud entity ID (null if failed)
    /// </summary>
    public Guid? CloudId { get; set; }

    public TransferItemStatus Status { get; set; }

    /// <summary>
    /// Display name for the item
    /// </summary>
    public string? Name { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime TransferredAt { get; set; }

    public TransferSession Session { get; set; } = null!;
}
