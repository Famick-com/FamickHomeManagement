using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// A per-user token for vCard feed export. External contact clients use this token
/// in the feed URL to access the user's contacts without authentication.
/// </summary>
public class UserContactVcfToken : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// The user who owns this token.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The unique token string used in the feed URL (e.g., /feed/{token}.vcf).
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Whether this token has been revoked. Revoked tokens return 404.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Optional label to help the user identify the token (e.g., "macOS Contacts", "Outlook").
    /// </summary>
    public string? Label { get; set; }

    #region Navigation Properties

    public virtual User? User { get; set; }

    #endregion
}
