using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to complete registration after email verification.
/// Can be completed with password or OAuth provider.
/// </summary>
public class CompleteRegistrationRequest
{
    /// <summary>
    /// The verification token from the email link
    /// </summary>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// User's first name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Password for the account (required if not using OAuth)
    /// </summary>
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
    public string? Password { get; set; }

    /// <summary>
    /// OAuth provider name (e.g., "Google", "Apple")
    /// If provided, providerToken must also be provided
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// OAuth provider token/code for verification
    /// </summary>
    public string? ProviderToken { get; set; }

    /// <summary>
    /// Session id from the passkey creation options issued for this registration.
    /// </summary>
    /// <remarks>
    /// Supplying this and <see cref="PasskeyAttestationResponse"/> finishes the signup with a passkey
    /// instead of a password — a third alternative beside password and OAuth, so the account is
    /// created with no password at all. Everything else about registration is unchanged: the email is
    /// still verified, the terms consent is still recorded, the household is still provisioned here.
    /// </remarks>
    public string? PasskeySessionId { get; set; }

    /// <summary>
    /// Serialized WebAuthn attestation for the passkey being registered with the new account.
    /// </summary>
    public string? PasskeyAttestationResponse { get; set; }
}
