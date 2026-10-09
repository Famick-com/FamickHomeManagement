namespace Famick.HomeManagement.Core.DTOs.Authentication;

/// <summary>
/// Response to a request for passkey creation options during registration.
/// </summary>
/// <remarks>
/// Shaped like the other registration responses — a <see cref="Success"/> flag plus a
/// <see cref="Message"/> — rather than throwing, because every reason this can fail is a normal
/// client-visible outcome: the link expired, the registration was already completed, the email was
/// never verified. Those all deserve the same treatment the password path already gives them.
/// </remarks>
public class PasskeySignupOptionsResponse
{
    /// <summary>Whether options were issued.</summary>
    public bool Success { get; set; }

    /// <summary>Explanation when <see cref="Success"/> is false.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// Serialized WebAuthn <c>PublicKeyCredentialCreationOptions</c> for the client to run.
    /// </summary>
    public string? Options { get; set; }

    /// <summary>
    /// Correlates the ceremony back to the server's challenge. Returned to the server as
    /// <c>CompleteRegistrationRequest.PasskeySessionId</c>.
    /// </summary>
    public string? SessionId { get; set; }
}
