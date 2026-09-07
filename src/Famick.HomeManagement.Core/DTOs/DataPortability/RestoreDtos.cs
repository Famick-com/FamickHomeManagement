using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.DataPortability;

/// <summary>
/// One restore, as the UI sees it.
/// </summary>
public sealed class RestoreSummary
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;

    public string? FileName { get; set; }
    public DateTime? ArchiveTakenAt { get; set; }

    public string? ProgressLabel { get; set; }
    public long ProgressCurrent { get; set; }
    public long ProgressTotal { get; set; }

    public int? PercentComplete =>
        ProgressTotal <= 0 ? null : (int)Math.Clamp(ProgressCurrent * 100 / ProgressTotal, 0, 100);

    /// <summary>Rows missing here — deleted since the backup, or never present.</summary>
    public int RestoredCount { get; set; }

    /// <summary>Rows present and untouched since the backup. Nothing to decide.</summary>
    public int UnchangedCount { get; set; }

    /// <summary>Rows edited here since the backup. The only ones needing a person.</summary>
    public int ChangedSinceCount { get; set; }

    public int InvalidCount { get; set; }

    /// <summary>What to do with changed-since rows unless a row says otherwise.</summary>
    public string ChangedSincePolicy { get; set; } = nameof(Domain.Enums.ChangedSincePolicy.KeepMine);

    public int AppliedInserted { get; set; }
    public int AppliedUpdated { get; set; }
    public int AppliedSkipped { get; set; }
    public int AppliedFailed { get; set; }

    /// <summary>
    /// Whether the household has a current export to fall back on. A restore overwrites, and there
    /// is no undo beyond an archive taken before it.
    /// </summary>
    public bool HasRecentBackup { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public bool AwaitingDecision => Status == nameof(HouseholdDataTransferStatus.AwaitingDecision);
}

/// <summary>A page of the rows that need a decision.</summary>
public sealed class RestoreReport
{
    public List<RestoreCategorySummary> Categories { get; set; } = [];
    public List<RestoreConflict> Conflicts { get; set; } = [];
    public int TotalConflicts { get; set; }
}

public sealed class RestoreCategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int Restored { get; set; }
    public int Unchanged { get; set; }
    public int ChangedSince { get; set; }
}

/// <summary>One row that exists in both, and differs.</summary>
public sealed class RestoreConflict
{
    public Guid ItemId { get; set; }
    public string Category { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public string? Label { get; set; }

    /// <summary>When the archive's copy was last changed.</summary>
    public DateTime? BackupUpdatedAt { get; set; }

    /// <summary>When this household's copy was last changed. The later of the two.</summary>
    public DateTime? CurrentUpdatedAt { get; set; }

    /// <summary>Null means "whatever the overall policy says".</summary>
    public string? Decision { get; set; }
}

public sealed class RestoreDecisionsRequest
{
    public string? ChangedSincePolicy { get; set; }

    /// <summary>Rows the user decided individually, overriding the policy.</summary>
    public List<RestoreOverride> Overrides { get; set; } = [];
}

public sealed record RestoreOverride(Guid ItemId, string Decision);
