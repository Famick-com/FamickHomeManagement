using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Users;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Invitations to join a household by email. Adding a member goes through here whenever the
/// server can send mail, so no account is created until the person accepts.
/// </summary>
public interface IHouseholdInvitationService
{
    /// <summary>Whether this server sends invitations (it can send email).</summary>
    bool InvitationsEnabled { get; }

    /// <summary>
    /// Invites the request's address to the current household and emails them. Responds the
    /// same whether or not the address already has an account.
    /// </summary>
    /// <exception cref="Exceptions.DuplicateEntityException">
    /// The address is already a member of, or has a pending invitation to, this household.
    /// </exception>
    Task<HouseholdInvitationDto> InviteAsync(CreateUserRequest request, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>The current household's pending invitations.</summary>
    Task<List<HouseholdInvitationDto>> GetPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Sends the invitation again with a new link and a fresh expiry.</summary>
    Task<HouseholdInvitationDto> ResendAsync(Guid invitationId, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>Withdraws a pending invitation; its link stops working.</summary>
    Task RevokeAsync(Guid invitationId, CancellationToken cancellationToken = default);

    /// <summary>What the link's holder sees before accepting. Anonymous.</summary>
    Task<InvitationDetailsResponse> GetDetailsAsync(string token, CancellationToken cancellationToken = default);

    /// <summary>Creates the account in the household with the chosen password. Anonymous.</summary>
    Task<AcceptInvitationResponse> AcceptAsync(AcceptInvitationRequest request, CancellationToken cancellationToken = default);
}
