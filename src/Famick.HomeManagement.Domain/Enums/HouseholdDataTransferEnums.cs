namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Which direction a household data transfer runs.
/// </summary>
public enum HouseholdDataTransferKind
{
    /// <summary>Building an archive of this household's data.</summary>
    Export = 0,

    /// <summary>Putting one back.</summary>
    Restore = 1,
}

/// <summary>
/// Where a household data transfer has got to.
/// </summary>
public enum HouseholdDataTransferStatus
{
    /// <summary>Accepted, not yet picked up by a worker.</summary>
    Queued = 0,

    /// <summary>A worker is building the archive, or reading and classifying one.</summary>
    Running = 1,

    /// <summary>
    /// A restore has classified everything and is waiting for the user. Nothing has been
    /// written to their household at this point, and nothing will be until they confirm.
    /// </summary>
    AwaitingDecision = 2,

    /// <summary>A confirmed restore is writing.</summary>
    Applying = 3,

    Completed = 4,
    Failed = 5,
    Cancelled = 6,

    /// <summary>The archive is past its ExpiresAt and is no longer downloadable.</summary>
    Expired = 7,
}

/// <summary>
/// What happens to a row that exists in both the archive and the household, and has been
/// edited in the household since the archive was taken.
/// </summary>
public enum ChangedSincePolicy
{
    /// <summary>
    /// Leave the household's version alone. The safe default: a restore should not silently
    /// discard work done since the backup was taken.
    /// </summary>
    KeepMine = 0,

    /// <summary>Overwrite with the archive's version.</summary>
    TakeBackup = 1,
}
