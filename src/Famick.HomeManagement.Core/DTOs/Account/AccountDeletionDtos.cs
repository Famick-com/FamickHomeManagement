namespace Famick.HomeManagement.Core.DTOs.Account;

/// <summary>
/// What a deletion request will actually destroy.
/// </summary>
public enum AccountDeletionScope
{
    /// <summary>
    /// Only the requesting user is removed. The household and everyone else in it stay.
    /// </summary>
    User = 0,

    /// <summary>
    /// The whole household goes: every member, and all of its data.
    /// </summary>
    Household = 1
}

/// <summary>
/// The state of a pending deletion, and enough context for the client to warn properly
/// before one is requested.
/// </summary>
public class AccountDeletionStatusDto
{
    /// <summary>
    /// Whether this deployment offers account deletion at all.
    /// </summary>
    /// <remarks>
    /// False on a self-hosted server, where the tenant is the whole installation and there
    /// is no in-app account creation to mirror. Clients should hide the entry point rather
    /// than offer a control that will be refused — the server answers this so the client
    /// does not have to infer it from how it happens to be configured.
    /// </remarks>
    public bool IsSupported { get; set; } = true;

    /// <summary>
    /// Whether a deletion is currently scheduled.
    /// </summary>
    public bool IsPending { get; set; }

    /// <summary>
    /// What this user's request destroys — or, when <see cref="IsPending"/>, what the
    /// scheduled deletion will destroy. Clients must show a materially stronger warning
    /// for <see cref="AccountDeletionScope.Household"/>.
    /// </summary>
    public AccountDeletionScope Scope { get; set; }

    /// <summary>
    /// When the request was made. Null when nothing is pending.
    /// </summary>
    public DateTime? RequestedAt { get; set; }

    /// <summary>
    /// When the data actually goes. Null when nothing is pending.
    /// </summary>
    public DateTime? PurgeAfter { get; set; }

    /// <summary>
    /// Household name, so a confirmation prompt can name what is about to be destroyed
    /// rather than saying "your data".
    /// </summary>
    public string? HouseholdName { get; set; }

    /// <summary>
    /// How many other people are in this household. Drives the warning copy: deleting a
    /// household with five members is not the same act as deleting an empty one.
    /// </summary>
    public int OtherMemberCount { get; set; }

    /// <summary>
    /// Present when a deletion was called off by signing in and the user has not been told
    /// yet. Null the rest of the time.
    /// </summary>
    public AccountDeletionCancelledNoticeDto? CancelledNotice { get; set; }
}

/// <summary>
/// Tells someone that the deletion they asked for is no longer going to happen.
/// </summary>
/// <remarks>
/// Signing in cancels a scheduled deletion, which means it gets cancelled by the ordinary
/// act of opening the app rather than by a decision. Someone who meant it to go ahead
/// would otherwise discover it only by noticing their data was still there.
/// </remarks>
public class AccountDeletionCancelledNoticeDto
{
    /// <summary>
    /// When the deletion had been requested.
    /// </summary>
    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When signing in cancelled it.
    /// </summary>
    public DateTime CancelledAt { get; set; }

    /// <summary>
    /// Whether the cancelled deletion covered the whole household.
    /// </summary>
    public bool WasHousehold { get; set; }
}

/// <summary>
/// Result of asking for a deletion.
/// </summary>
public class AccountDeletionRequestResultDto
{
    public AccountDeletionScope Scope { get; set; }

    public DateTime RequestedAt { get; set; }

    /// <summary>
    /// When the data is destroyed. Signing in before this cancels the deletion, and the
    /// client should say so plainly — it is the whole reason the delay exists.
    /// </summary>
    public DateTime PurgeAfter { get; set; }

    /// <summary>
    /// Members losing access alongside the requester. Zero for a member deleting only
    /// themselves.
    /// </summary>
    public int OtherMembersAffected { get; set; }
}
