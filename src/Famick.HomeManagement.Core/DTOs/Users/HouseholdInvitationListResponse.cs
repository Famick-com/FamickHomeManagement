namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// The household's pending invitations, and whether this server invites at all.
/// </summary>
public class HouseholdInvitationListResponse
{
    /// <summary>
    /// False when the server cannot send email. Adding a member then creates the account
    /// directly with a temporary password instead.
    /// </summary>
    public bool InvitationsEnabled { get; set; }

    public List<HouseholdInvitationDto> Invitations { get; set; } = new();
}
