using System.ComponentModel.DataAnnotations;
using Famick.HomeManagement.Core.Validators.Common;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request to complete registration after email verification.
/// Completed with a password or a passkey.
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
    /// Password for the account (required unless a passkey is supplied)
    /// </summary>
    [AcceptablePassword]
    public string? Password { get; set; }

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
