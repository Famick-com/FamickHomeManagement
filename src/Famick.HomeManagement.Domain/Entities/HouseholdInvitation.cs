using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// An admin's invitation for someone to join their household by email.
/// </summary>
/// <remarks>
/// <para>
/// Adding a member creates one of these and no user. A new address gets a link to choose a
/// password, and accepting it creates the user in this household. An address that already has
/// an account is told about the invitation but cannot accept it yet: one person in several
/// households is a later feature. Either way the admin sees the same pending invitation, so
/// adding a member never reveals whether an email already has an account.
/// </para>
/// <para>
/// Only the SHA-256 of the token is stored, the same as password reset tokens.
/// </para>
/// </remarks>
public class HouseholdInvitation : BaseEntity, ITenantEntity
{
    /// <summary>The household the person is invited to.</summary>
    public Guid TenantId { get; set; }

    /// <summary>Normalised with <c>EmailAddress.Normalize</c>.</summary>
    public string Email { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    /// <summary>The roles the user gets on accepting.</summary>
    public List<Role> Roles { get; set; } = new();

    /// <summary>An existing household contact to link the new user to, if the admin chose one.</summary>
    public Guid? ContactId { get; set; }

    public Guid InvitedByUserId { get; set; }

    /// <summary>SHA-256 of the token in the emailed link, base64.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set when the invitation was accepted; the user it created is <see cref="AcceptedUserId"/>.</summary>
    public DateTime? AcceptedAt { get; set; }

    public Guid? AcceptedUserId { get; set; }

    /// <summary>Set when an admin withdrew the invitation.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>Not accepted, not revoked and not expired.</summary>
    public bool IsPending(DateTime now) => AcceptedAt == null && RevokedAt == null && ExpiresAt > now;
}
