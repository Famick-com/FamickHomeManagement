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
    /// User's phone number for SMS notifications (E.164 format, e.g., "+15551234567")
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Link to the user's Contact record (1:1 relationship)
    /// </summary>
    public Guid? ContactId { get; set; }

    /// <summary>
    /// When this user asked for their account to be deleted, or null if they have not.
    /// Set only for a member leaving a household — an admin deleting the whole household
    /// is recorded on the <see cref="Tenant"/> instead.
    /// </summary>
    public DateTime? DeletionRequestedAt { get; set; }

    /// <summary>
    /// The instant after which this user may be permanently removed. Signing in again
    /// before then clears both this and <see cref="DeletionRequestedAt"/>.
    /// </summary>
    public DateTime? DeletionPurgeAfter { get; set; }

    /// <summary>
    /// When the final warning email was sent, so the job sends it once rather than on
    /// every run through the last three days.
    /// </summary>
    public DateTime? DeletionReminderSentAt { get; set; }

    /// <summary>
    /// Set when a sign-in cancelled a pending deletion, and cleared once the client has
    /// told the user. Null means there is nothing to tell them.
    /// </summary>
    /// <remarks>
    /// A deletion can be cancelled without anyone deciding to — signing in is enough —
    /// so someone who meant it to go ahead would otherwise find out only when the data
    /// was still there weeks later.
    /// </remarks>
    public DateTime? DeletionCancelledNoticeAt { get; set; }

    /// <summary>
    /// When the cancelled deletion had originally been requested, so the notice can say
    /// which one it means.
    /// </summary>
    public DateTime? DeletionCancelledNoticeRequestedAt { get; set; }

    /// <summary>
    /// Whether the cancelled deletion was of the whole household. Recorded because once
    /// it is cancelled there is nothing left to infer it from.
    /// </summary>
    public bool DeletionCancelledNoticeWasHousehold { get; set; }

    /// <summary>
    /// Phase 4 chunk 4.D — Canonical form (<c>scheme://host[:port]</c>) of
    /// the most recent local-server URL the server delivered to this user
    /// on a successful login. Null until the first self-hosted login;
    /// subsequent logins compare against this value and emit a
    /// <see cref="Famick.HomeManagement.Domain.Enums.UserAuditAction.LocalServerChanged"/>
    /// audit-log entry when it differs. Mobile clients use the same
    /// value (delivered on <c>LoginResponse.LocalServer</c>) to drive the
    /// change-confirmation prompt.
    /// </summary>
    public string? LastDeliveredLocalServer { get; set; }

    // Navigation properties
    // Note: Tenant navigation property is cloud-specific and defined in homemanagement-cloud
    public virtual Contact? Contact { get; set; }
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
    public ICollection<UserPasskeyCredential> PasskeyCredentials { get; set; } = new List<UserPasskeyCredential>();
}
