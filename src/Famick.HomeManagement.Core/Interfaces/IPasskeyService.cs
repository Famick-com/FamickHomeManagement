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
