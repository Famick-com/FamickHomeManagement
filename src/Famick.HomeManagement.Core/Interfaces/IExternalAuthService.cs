using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for external authentication providers (Google, Apple, OpenID Connect)
/// </summary>
public interface IExternalAuthService
{
    /// <summary>
    /// Gets list of enabled external authentication providers
    /// </summary>
    Task<List<ExternalAuthProviderDto>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates OAuth authorization URL for a provider (login flow)
    /// </summary>
    /// <param name="provider">Provider name (Google, Apple, OIDC)</param>
    /// <param name="redirectUri">Callback URI after OAuth</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization URL and state for CSRF protection</returns>
    Task<ExternalAuthChallengeResponse> GetAuthorizationUrlAsync(
        string provider,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates OAuth authorization URL for linking a provider to existing account
    /// </summary>
    /// <param name="userId">Current user ID to link to</param>
    /// <param name="provider">Provider name (Google, Apple, OIDC)</param>
    /// <param name="redirectUri">Callback URI after OAuth</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authorization URL and state for CSRF protection</returns>
    Task<ExternalAuthChallengeResponse> GetLinkAuthorizationUrlAsync(
        Guid userId,
        string provider,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes OAuth callback, creating or linking user account
    /// </summary>
    /// <param name="provider">Provider name</param>
    /// <param name="request">Callback request with code and state</param>
    /// <param name="redirectUri">Redirect URI used in authorization request</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="deviceInfo">Device/User-Agent information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with tokens</returns>
    Task<LoginResponse> ProcessCallbackAsync(
        string provider,
        ExternalAuthCallbackRequest request,
        string redirectUri,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Links an external provider to an existing user account
    /// </summary>
    /// <param name="userId">Current user ID</param>
    /// <param name="provider">Provider name</param>
    /// <param name="request">Link request with code and state</param>
    /// <param name="redirectUri">Redirect URI used in authorization request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<LinkedAccountDto> LinkProviderAsync(
        Guid userId,
        string provider,
        ExternalAuthLinkRequest request,
        string redirectUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unlinks an external provider from user account
    /// </summary>
    /// <param name="userId">Current user ID</param>
    /// <param name="provider">Provider name to unlink</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UnlinkProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets list of linked external accounts for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of linked accounts</returns>
    Task<List<LinkedAccountDto>> GetLinkedAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes native Apple Sign in from iOS devices
    /// </summary>
    /// <param name="request">Request containing identity token from native Sign in with Apple</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="deviceInfo">Device information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with tokens</returns>
    Task<LoginResponse> ProcessNativeAppleSignInAsync(
        NativeAppleSignInRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes native Google Sign in from iOS and Android devices
    /// </summary>
    /// <param name="request">Request containing ID token from native Google Sign-In SDK</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="deviceInfo">Device information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with tokens</returns>
    Task<LoginResponse> ProcessNativeGoogleSignInAsync(
        NativeGoogleSignInRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default);
}
