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
