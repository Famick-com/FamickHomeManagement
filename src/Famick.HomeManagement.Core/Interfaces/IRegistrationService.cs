using Famick.HomeManagement.Core.DTOs.Authentication;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for user registration with email verification (mobile onboarding flow)
/// </summary>
public interface IRegistrationService
{
    /// <summary>
    /// Starts the registration process by sending a verification email.
    /// Creates a pending registration record.
    /// </summary>
    /// <param name="request">The registration request with household name and email</param>
    /// <param name="ipAddress">The IP address of the client</param>
    /// <param name="deviceInfo">Device/User-Agent information</param>
    /// <param name="baseUrl">Base URL for constructing the verification link</param>
    /// <param name="webVerificationPath">
    /// Path on this host that verifies in a browser, for example <c>/verify-email</c>. When set,
    /// the emailed link is an https URL rather than a <c>famick://</c> deep link, so it opens in
    /// a browser for anyone without the app installed. Leave it null to send the deep link.
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating email was sent</returns>
    Task<StartRegistrationResponse> StartRegistrationAsync(
        StartRegistrationRequest request,
        string ipAddress,
        string deviceInfo,
        string baseUrl,
        string? webVerificationPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies an email address using the token from the verification email.
    /// Marks the pending registration as verified.
    /// </summary>
    /// <param name="request">The verification request with token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating verification status</returns>
    Task<VerifyEmailResponse> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the registration by creating the user account and tenant.
    /// Requires email to be verified first.
    /// </summary>
    /// <param name="request">The completion request with password or OAuth details</param>
    /// <param name="ipAddress">The IP address of the client</param>
    /// <param name="deviceInfo">Device/User-Agent information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response with tokens and user/tenant info</returns>
    Task<CompleteRegistrationResponse> CompleteRegistrationAsync(
        CompleteRegistrationRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues WebAuthn creation options so a verified registration can be completed with a passkey
    /// instead of a password.
    /// </summary>
    /// <param name="token">The verification token from the email link.</param>
    /// <param name="displayName">Name to show in the OS credential manager, if known.</param>
    /// <param name="deviceName">Optional name for the credential.</param>
    /// <param name="clientType">
    /// <c>native</c> when the mobile app is driving the ceremony, so the server uses the relying
    /// party the app is associated with. See <c>PasskeyClientType</c>.
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Creation options and a session id, or a failure describing why not.</returns>
    /// <remarks>
    /// Lives here rather than on the passkey controller because the authorization is the registration
    /// token: holding a valid, verified, uncompleted token is what proves the caller owns the email
    /// and is entitled to options for an account that does not exist yet. The passkey service's own
    /// anonymous registration path is refused on any server that has users, and must stay that way —
    /// it created accounts with no email verification, consent or household.
    /// </remarks>
    Task<PasskeySignupOptionsResponse> GetPasskeySignupOptionsAsync(
        string token,
        string? displayName,
        string? deviceName,
        string? clientType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resends the verification email for a pending registration.
    /// </summary>
    /// <param name="email">The email address to resend verification to</param>
    /// <param name="baseUrl">Base URL for constructing the verification link</param>
    /// <param name="webVerificationPath">
    /// Path on this host that verifies in a browser, for example <c>/verify-email</c>. When set,
    /// the emailed link is an https URL rather than a <c>famick://</c> deep link, so it opens in
    /// a browser for anyone without the app installed. Leave it null to send the deep link.
    /// </param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating email was sent</returns>
    Task<StartRegistrationResponse> ResendVerificationEmailAsync(
        string email,
        string baseUrl,
        string? webVerificationPath = null,
        CancellationToken cancellationToken = default);
}
