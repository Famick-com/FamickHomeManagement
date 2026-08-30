namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// What deleting will destroy. Mirrors the server's AccountDeletionScope.
/// </summary>
public enum AccountDeletionScope
{
    /// <summary>
    /// Only this account. The household and everyone else in it stay.
    /// </summary>
    User = 0,

    /// <summary>
    /// The whole household: every member, and all of its data.
    /// </summary>
    Household = 1
}

/// <summary>
/// What a deletion would destroy, and whether one is already scheduled.
/// </summary>
public class AccountDeletionStatusMobile
{
    public bool IsPending { get; set; }
    public AccountDeletionScope Scope { get; set; }
    public DateTime? RequestedAt { get; set; }
    public DateTime? PurgeAfter { get; set; }

    /// <summary>
    /// Named in the confirmation prompt, so the warning says which household is going
    /// rather than "your data".
    /// </summary>
    public string? HouseholdName { get; set; }

    /// <summary>
    /// Other people who lose access. Deleting a household with four other members is not
    /// the same act as deleting an empty one, and the prompt should not read as if it is.
    /// </summary>
    public int OtherMemberCount { get; set; }
}

/// <summary>
/// Confirmation that a deletion has been scheduled.
/// </summary>
public class AccountDeletionResultMobile
{
    public AccountDeletionScope Scope { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime PurgeAfter { get; set; }
    public int OtherMembersAffected { get; set; }
}
