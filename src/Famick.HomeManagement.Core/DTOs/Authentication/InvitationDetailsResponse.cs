namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// What the person holding an invitation link sees before accepting.
/// </summary>
public class InvitationDetailsResponse
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? HouseholdName { get; set; }

    /// <summary>
    /// False when the address already has an account, which cannot join a second household yet.
    /// Only the link's holder sees this, and the link went to that address.
    /// </summary>
    public bool CanAccept { get; set; }
}
