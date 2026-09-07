using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// One run of exporting a household's data, or restoring it.
/// </summary>
/// <remarks>
/// <para>
/// State lives here rather than in the worker's memory. The cloud runs more than one instance,
/// so a progress field on a service object is invisible to the request that polls it and is lost
/// entirely when a deploy recycles the task mid-run.
/// </para>
/// <para>
/// This is a tenant entity on purpose: an export is household data, and being an
/// <c>ITenantEntity</c> means the household purge already removes it in the right order without
/// anybody having to remember.
/// </para>
/// </remarks>
public class HouseholdDataTransfer : BaseTenantEntity
{
    public HouseholdDataTransferKind Kind { get; set; }

    public HouseholdDataTransferStatus Status { get; set; }

    /// <summary>Who asked for it. Where the "your export is ready" email goes.</summary>
    public Guid RequestedByUserId { get; set; }
    public User? RequestedByUser { get; set; }

    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Touched as the worker runs. A Running row whose heartbeat has gone stale belongs to a
    /// worker that died; without this a killed task blocks the household forever.
    /// </summary>
    public DateTime? HeartbeatAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// When the archive stops being downloadable. Enforced by the API rather than left to a
    /// storage lifecycle rule, so the answer does not depend on which side deleted first.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Whether photos and documents were included.</summary>
    public bool IncludeFiles { get; set; } = true;

    #region Archive

    public string? ArchiveFileName { get; set; }
    public long? ArchiveBytes { get; set; }
    public string? ArchiveSha256 { get; set; }

    /// <summary>The manifest, kept so the UI can report counts without re-opening the zip.</summary>
    public string? ManifestJson { get; set; }

    #endregion

    #region Restore

    /// <summary>The name it is stored under — chosen here, never the one that arrived.</summary>
    public string? UploadFileName { get; set; }

    /// <summary>
    /// The name the file arrived with, for showing the user which one they picked.
    /// </summary>
    /// <remarks>
    /// Display only. It is attacker-controlled, so it never reaches a filesystem path, a storage
    /// key or a URL.
    /// </remarks>
    public string? OriginalUploadFileName { get; set; }
    public long? UploadBytes { get; set; }

    public ChangedSincePolicy ChangedSincePolicy { get; set; } = ChangedSincePolicy.KeepMine;

    /// <summary>
    /// How many rows fell into each classification, as a small JSON object.
    /// </summary>
    /// <remarks>
    /// Kept here rather than counted from the item rows, because only rows needing a decision get
    /// an item — the unchanged majority leaves no trace to count.
    /// </remarks>
    public string? RestoreCountsJson { get; set; }

    #endregion

    #region Progress

    /// <summary>A short human-readable label, e.g. the table being written.</summary>
    public string? ProgressLabel { get; set; }
    public long ProgressCurrent { get; set; }
    public long ProgressTotal { get; set; }

    #endregion

    /// <summary>A stable code the UI can branch on, separate from the human message.</summary>
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public ICollection<HouseholdDataTransferItem> Items { get; set; } = new List<HouseholdDataTransferItem>();
}
