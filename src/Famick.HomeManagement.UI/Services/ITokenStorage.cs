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
    Task ClearTokensAsync();
}
