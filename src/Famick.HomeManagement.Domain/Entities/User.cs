using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a user within a tenant
/// </summary>
public class User : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// When the user accepted the Terms of Service and Privacy Policy (null = not yet accepted)
    /// </summary>
    public DateTime? TermsAcceptedAt { get; set; }

    /// <summary>
    /// Version of the terms the user accepted (matches effective date, e.g. "2026-02-19")
    /// </summary>
    public string? TermsVersion { get; set; }

    /// <summary>
    /// IP address from which terms were accepted
    /// </summary>
    public string? TermsAcceptedIpAddress { get; set; }

    /// <summary>
    /// User's preferred language code (e.g., "en", "es", "fr")
    /// </summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>
    /// Link to the user's Contact record (1:1 relationship)
    /// </summary>
    public Guid? ContactId { get; set; }

    // Navigation properties
    // Note: Tenant navigation property is cloud-specific and defined in homemanagement-cloud
    public virtual Contact? Contact { get; set; }
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
    public ICollection<UserPasskeyCredential> PasskeyCredentials { get; set; } = new List<UserPasskeyCredential>();
}
