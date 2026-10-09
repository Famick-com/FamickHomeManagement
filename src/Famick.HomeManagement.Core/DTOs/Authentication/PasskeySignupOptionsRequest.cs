using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Request for passkey creation options during registration.
/// </summary>
public class PasskeySignupOptionsRequest
{
    /// <summary>
    /// The verification token from the email link.
    /// </summary>
    /// <remarks>
    /// This is the authorization for the whole request. The email address the passkey is created
    /// against is read from the token server-side and never taken from the client, so a caller
    /// cannot obtain options — and from them an account — for an address they do not own.
    /// </remarks>
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Name shown in the OS credential manager. Falls back to the verified email address.
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Optional name to store against the credential, normally the device name.
    /// </summary>
    [MaxLength(255)]
    public string? DeviceName { get; set; }

    /// <summary>
    /// <c>native</c> when the mobile app is driving the ceremony — see <c>PasskeyClientType</c>.
    /// Absent or unrecognised means a browser.
    /// </summary>
    public string? ClientType { get; set; }
}
