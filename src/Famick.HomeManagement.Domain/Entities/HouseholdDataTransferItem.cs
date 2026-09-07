using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// One row of a restore that either needs a decision or has something to report.
/// </summary>
/// <remarks>
/// Deliberately not written for every row. A restore of a large household is mostly rows that
/// match and need nothing said about them; storing one of these per row would make the bookkeeping
/// larger than the data. Only rows the user has to decide about, and rows that failed, land here.
/// </remarks>
public class HouseholdDataTransferItem : BaseTenantEntity
{
    public Guid TransferId { get; set; }
    public HouseholdDataTransfer? Transfer { get; set; }

    /// <summary>The entity type this row belongs to, as it appears in the manifest.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>The row's primary key in the archive. Preserved on restore, so also its key here.</summary>
    public Guid SourceId { get; set; }

    /// <summary>Something a person can recognise — a product name, a contact's name.</summary>
    public string? Label { get; set; }

    public RestoreClassification Classification { get; set; }

    public DateTime? SourceUpdatedAt { get; set; }
    public DateTime? TargetUpdatedAt { get; set; }

    /// <summary>
    /// Null means "whatever the transfer's policy says". Set only when the user overrode this
    /// particular row, so changing the overall policy still moves everything they did not touch.
    /// </summary>
    public ChangedSincePolicy? Decision { get; set; }

    public RestoreItemStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// What a staged row turned out to be when checked against the household.
/// </summary>
public enum RestoreClassification
{
    /// <summary>Not in the household — deleted since the archive was taken, or never there.</summary>
    Restored = 0,

    /// <summary>Present and not touched since the archive was taken.</summary>
    Unchanged = 1,

    /// <summary>Present, and edited since. The only class that needs a human.</summary>
    ChangedSince = 2,

    /// <summary>Unreadable, or referencing something that cannot be resolved.</summary>
    Invalid = 3,
}

public enum RestoreItemStatus
{
    Pending = 0,
    Inserted = 1,
    Updated = 2,
    Skipped = 3,
    Failed = 4,
}
