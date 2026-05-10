namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Platform-specific token storage abstraction.
/// Web uses localStorage, MAUI uses SecureStorage.
/// </summary>
public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();
    Task<string?> GetRefreshTokenAsync();
    Task SetTokensAsync(string accessToken, string refreshToken);

    /// <summary>
    /// Phase 2.5 — swap only the access token without rotating the refresh
    /// token. Used by the step-up reauth flow which returns a fresh access
    /// token (with refreshed <c>auth_time</c>) while preserving the existing
    /// refresh-token family.
    /// </summary>
    Task SetAccessTokenAsync(string accessToken);

    Task ClearTokensAsync();
}
