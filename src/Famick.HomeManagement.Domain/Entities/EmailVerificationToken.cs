namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Email verification token for new user registration.
/// Stores pending registration data until email is verified and account is created.
/// </summary>
public class EmailVerificationToken : BaseEntity
{
    /// <summary>
    /// Email address being verified
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Name for the household/tenant to be created
    /// </summary>
    public string HouseholdName { get; set; } = string.Empty;

    /// <summary>
    /// Hashed verification token (SHA256 of the actual token)
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// When this verification token expires (24 hours from creation)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When the email was verified (null if not yet verified)
    /// </summary>
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// When the registration was completed (null if not yet completed)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// IP address of the client that initiated registration
    /// </summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Device info of the client that initiated registration
    /// </summary>
    public string DeviceInfo { get; set; } = string.Empty;

    // Computed properties

    /// <summary>
    /// Whether the email has been verified
    /// </summary>
    public bool IsVerified => VerifiedAt.HasValue;

    /// <summary>
    /// Whether the registration has been completed
    /// </summary>
    public bool IsCompleted => CompletedAt.HasValue;

    /// <summary>
    /// Whether this token has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>
    /// Whether this token is valid for verification (not verified, not completed, not expired)
    /// </summary>
    public bool IsValidForVerification => !IsVerified && !IsCompleted && !IsExpired;

    /// <summary>
    /// Whether this token is valid for completing registration (verified, not completed, not expired)
    /// </summary>
    public bool IsValidForCompletion => IsVerified && !IsCompleted && !IsExpired;
}
