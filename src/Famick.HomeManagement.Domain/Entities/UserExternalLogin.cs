using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an external authentication provider linked to a user account.
/// Supports providers like Apple, Google, and generic OpenID Connect.
/// </summary>
public class UserExternalLogin : BaseEntity, ITenantEntity
{
    /// <summary>
    /// ID of the tenant this external login belongs to
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// ID of the user this external login is linked to
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The external authentication provider name (e.g., "Apple", "Google", "OIDC")
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier from the provider (typically the 'sub' claim)
    /// </summary>
    public string ProviderUserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name from the provider (e.g., user's full name)
    /// </summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>
    /// Email address from the provider
    /// </summary>
    public string? ProviderEmail { get; set; }

    /// <summary>
    /// When this external login was last used for authentication
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    // Navigation properties

    /// <summary>
    /// The user this external login is linked to
    /// </summary>
    public User User { get; set; } = null!;
}
