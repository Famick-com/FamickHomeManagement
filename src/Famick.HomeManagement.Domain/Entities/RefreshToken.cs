using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Refresh token entity for managing user sessions and token rotation
/// </summary>
public class RefreshToken : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID of the user this refresh token belongs to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// ID of the tenant this refresh token belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Hashed refresh token (SHA256 of the actual token)
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// When this refresh token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this refresh token was revoked (null if still active)
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// ID of the token that replaced this one (for token rotation tracking)
    /// </summary>
    public Guid? ReplacedByTokenId { get; set; }

    /// <summary>
    /// Device/User-Agent information for security tracking
    /// </summary>
    public string DeviceInfo { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the client that requested this token
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Whether the user selected "Remember Me" during login.
    /// This preference is preserved during token refresh to maintain extended expiration.
    /// </summary>
    public bool RememberMe { get; set; }

    /// <summary>
    /// Whether this token has been explicitly revoked
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// Identifies the family of refresh tokens this one belongs to. Set on the
    /// initial issuance (login or step-up re-auth) and inherited by every descendant
    /// produced via rotation. Reuse-detection bulk-revokes the entire family by FamilyId.
    /// </summary>
    public Guid FamilyId { get; set; }

    /// <summary>
    /// Time of the most recent first-factor authentication that produced this family.
    /// Set on issuance, copied forward verbatim on rotation, refreshed only on a fresh
    /// login or step-up re-auth. Read by the refresh path so the new access token's
    /// auth_time claim reflects the original authentication, not the time of the rotation.
    /// </summary>
    public DateTime AuthTime { get; set; }

    // Navigation properties
    /// <summary>
    /// The user this refresh token belongs to
    /// </summary>
    public User User { get; set; } = null!;

    // Note: Tenant navigation property is cloud-specific and defined in homemanagement-cloud

    /// <summary>
    /// The token that replaced this one (if any)
    /// </summary>
    public RefreshToken? ReplacedByToken { get; set; }

    // Computed properties
    /// <summary>
    /// Whether this token has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Whether this token is active (not revoked and not expired)
    /// </summary>
    public bool IsActive => !IsRevoked && !IsExpired;
}
