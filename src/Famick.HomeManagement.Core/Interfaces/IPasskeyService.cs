using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for WebAuthn/FIDO2 passkey authentication
/// </summary>
public interface IPasskeyService
{
    /// <summary>
    /// Gets whether passkey authentication is enabled
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets whether ceremonies driven by the native mobile app can be served — that is, whether a
    /// native relying-party ID and origin list are configured.
    /// </summary>
    /// <remarks>
    /// Surfaced to clients through the auth-configuration endpoint. The mobile app gates its passkey
    /// UI on this rather than on its own connection mode, so a server that cannot complete the
    /// ceremony never offers one, and a server that gains the capability lights it up without an app
    /// release.
    /// </remarks>
    bool IsNativeEnabled { get; }

    /// <summary>
    /// Gets registration options for creating a new passkey
    /// </summary>
    /// <param name="userId">Existing user ID (null for new user registration)</param>
    /// <param name="request">Registration options request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>WebAuthn registration options</returns>
    Task<PasskeyRegisterOptionsResponse> GetRegisterOptionsAsync(
        Guid? userId,
        PasskeyRegisterOptionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies and completes passkey registration
    /// </summary>
    /// <param name="userId">Existing user ID (null for new user registration)</param>
    /// <param name="request">Verification request with attestation response</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="deviceInfo">Device/User-Agent information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration result with optional tokens for new users</returns>
    Task<PasskeyRegisterVerifyResponse> VerifyRegisterAsync(
        Guid? userId,
        PasskeyRegisterVerifyRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mints WebAuthn creation options for a verified-but-not-yet-created signup.
    /// </summary>
    /// <param name="email">The verified email address the account will be created for.</param>
    /// <param name="displayName">Name shown in the OS credential manager.</param>
    /// <param name="deviceName">Optional name for the credential.</param>
    /// <param name="native">Whether to use the native relying-party configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Separate from <see cref="GetRegisterOptionsAsync"/> on purpose. That method's anonymous branch
    /// is refused once a server has users, because it created an account outright with no email
    /// verification, consent or household provisioning. This one performs no authorization of its own
    /// — the caller must already have established that the email is verified, which
    /// <c>IRegistrationService</c> does by validating the registration token — and it creates
    /// nothing. Its session is held under a distinct cache key so it cannot be redeemed through
    /// <see cref="VerifyRegisterAsync"/>, which would be a way back to creating a user without a
    /// token.
    /// </remarks>
    Task<PasskeyRegisterOptionsResponse> CreatePendingSignupOptionsAsync(
        string email,
        string? displayName,
        string? deviceName,
        bool native,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the attestation from a pending-signup ceremony and returns the credential, without
    /// storing it or creating a user.
    /// </summary>
    /// <param name="sessionId">Session id from <see cref="CreatePendingSignupOptionsAsync"/>.</param>
    /// <param name="attestationResponse">Serialized attestation from the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The verified credential, including the server-minted user handle the new user's id must take.
    /// </returns>
    /// <remarks>
    /// Persistence is the caller's job so that the user, the household, the role, the terms
    /// acceptance and the credential are all written in one transaction by the service that owns
    /// registration. A failure anywhere then leaves nothing behind, rather than a credential with no
    /// account or an account with no way to sign in.
    /// </remarks>
    Task<PasskeyVerifiedCredential> VerifyPendingSignupAsync(
        string sessionId,
        string attestationResponse,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues the session for a user that a passkey-first signup has just created.
    /// </summary>
    /// <param name="userId">The newly created user.</param>
    /// <param name="ipAddress">Client IP, recorded on the refresh token.</param>
    /// <param name="deviceInfo">Device/User-Agent, recorded on the refresh token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// A passkey-first account has no password, so the ordinary
    /// <c>IAuthenticationService.LoginAsync</c> cannot be used to sign the user in after
    /// registration — and asking them to go back to the sign-in screen seconds after creating an
    /// account would be a poor ending to the flow. This reuses the same token issuance the passkey
    /// login path uses, rather than adding a third place that mints and persists refresh tokens.
    ///
    /// <para>
    /// <b>Caller contract:</b> only for a user the caller has just created in the same operation,
    /// after verifying their attestation. It performs no credential check of its own — it cannot,
    /// there is nothing yet to check against — so calling it in any other context would be issuing a
    /// session to an unauthenticated request.
    /// </para>
    /// </remarks>
    Task<LoginResponse> IssuePendingSignupSessionAsync(
        Guid userId,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets authentication options for passkey login
    /// </summary>
    /// <param name="request">Authentication options request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>WebAuthn authentication options</returns>
    Task<PasskeyAuthenticateOptionsResponse> GetAuthenticateOptionsAsync(
        PasskeyAuthenticateOptionsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies passkey authentication and returns tokens
    /// </summary>
    /// <param name="request">Verification request with assertion response</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="deviceInfo">Device/User-Agent information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with tokens</returns>
    Task<LoginResponse> VerifyAuthenticateAsync(
        PasskeyAuthenticateVerifyRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phase 3 — family-preserving passkey reauth. Verifies a WebAuthn
    /// assertion belongs to the specified user, updates the credential's
    /// signature counter + last-used timestamp, and returns. Unlike
    /// <see cref="VerifyAuthenticateAsync"/>, this method does NOT issue
    /// tokens — the caller (AuthApiController.ReauthPasskey) generates a
    /// fresh access token while leaving the refresh-token family intact,
    /// matching the password reauth shape.
    /// </summary>
    /// <param name="userId">The currently-authenticated user; the assertion's
    /// credential must belong to this user or verification fails.</param>
    /// <param name="request">Verification request with assertion response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task VerifyReauthAssertionAsync(
        Guid userId,
        PasskeyAuthenticateVerifyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of registered passkeys for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of passkey credentials</returns>
    Task<List<PasskeyCredentialDto>> GetCredentialsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a passkey credential
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="credentialId">Credential ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a passkey credential
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="credentialId">Credential ID to rename</param>
    /// <param name="request">Rename request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RenameCredentialAsync(
        Guid userId,
        Guid credentialId,
        PasskeyRenameRequest request,
        CancellationToken cancellationToken = default);
}
