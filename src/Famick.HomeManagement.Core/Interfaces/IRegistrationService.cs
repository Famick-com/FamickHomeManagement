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
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating email was sent</returns>
    Task<StartRegistrationResponse> StartRegistrationAsync(
        StartRegistrationRequest request,
        string ipAddress,
        string deviceInfo,
        string baseUrl,
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
    /// Resends the verification email for a pending registration.
    /// </summary>
    /// <param name="email">The email address to resend verification to</param>
    /// <param name="baseUrl">Base URL for constructing the verification link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating email was sent</returns>
    Task<StartRegistrationResponse> ResendVerificationEmailAsync(
        string email,
        string baseUrl,
        CancellationToken cancellationToken = default);
}
