using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Password reset token entity for secure password recovery
/// </summary>
public class PasswordResetToken : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID of the user requesting password reset
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// ID of the tenant this token belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Hashed reset token (SHA256 of the actual token)
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// When this reset token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this token was used (null if not yet used)
    /// </summary>
    public DateTime? UsedAt { get; set; }

    /// <summary>
    /// IP address of the client that requested the reset
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    // Navigation properties
    /// <summary>
    /// The user this reset token belongs to
    /// </summary>
    public User User { get; set; } = null!;

    // Computed properties
    /// <summary>
    /// Whether this token has been used
    /// </summary>
    public bool IsUsed => UsedAt.HasValue;

    /// <summary>
    /// Whether this token has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Whether this token is valid (not used and not expired)
    /// </summary>
    public bool IsValid => !IsUsed && !IsExpired;
}
