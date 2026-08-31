using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

/// <summary>
/// Shared by all four account-deletion emails: scheduled, cancelled, the three-day
/// reminder, and the confirmation that it is done.
/// </summary>
/// <remarks>
/// Dates arrive pre-formatted. Mustache cannot format a <see cref="DateTime"/>, and the
/// alternative — rendering whatever ToString the culture happens to pick — is how an email
/// warning someone their data dies on a particular day ends up printing an ambiguous one.
/// </remarks>
public class AccountDeletionData : IMessageData
{
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// True when the whole household is going, not just one account. Templates branch on
    /// this because the two are not the same news: one is losing your login, the other is
    /// losing everything the household ever recorded.
    /// </summary>
    public bool IsHousehold { get; set; }

    /// <summary>
    /// The household's name, for the household case. Empty when it has none.
    /// </summary>
    public string HouseholdName { get; set; } = string.Empty;

    /// <summary>
    /// True when this person did not ask for the deletion — a member of a household an
    /// admin is closing. They cannot cancel it, so the template must not tell them to
    /// sign in and everything will be fine.
    /// </summary>
    public bool IsBystander { get; set; }

    /// <summary>
    /// When the deletion was requested, e.g. "30 August 2026".
    /// </summary>
    public string RequestedOn { get; set; } = string.Empty;

    /// <summary>
    /// The date the data is destroyed, e.g. "29 September 2026".
    /// </summary>
    public string DeletedOn { get; set; } = string.Empty;

    /// <summary>
    /// Days left, for the reminder.
    /// </summary>
    public int DaysRemaining { get; set; }
}
