namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Secure token storage using MAUI SecureStorage.
/// Provides platform-native secure storage for JWT tokens.
/// On iOS, also writes tokens to a shared keychain for widget extension access.
/// </summary>
public class TokenStorage
{
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    private readonly ApiSettings _apiSettings;

    public TokenStorage(ApiSettings apiSettings)
    {
        _apiSettings = apiSettings;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(AccessTokenKey).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Synchronous version for startup checks. Uses Task.Run to avoid deadlocks.
    /// </summary>
    public string? GetAccessToken()
    {
        try
        {
            // Use a separate thread to avoid deadlock on UI thread
            return Task.Run(async () => await SecureStorage.Default.GetAsync(AccessTokenKey)).Result;
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await SecureStorage.Default.GetAsync(RefreshTokenKey).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string refreshToken)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken).ConfigureAwait(false);
            await SecureStorage.Default.SetAsync(RefreshTokenKey, refreshToken).ConfigureAwait(false);
#if IOS
            Platforms.iOS.SharedKeychainService.SetSharedTokens(accessToken, refreshToken, _apiSettings.BaseUrl);
#endif
        }
        catch
        {
            // Handle storage exceptions (e.g., secure storage not available)
        }
    }

    public Task ClearTokensAsync()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
#if IOS
            Platforms.iOS.SharedKeychainService.ClearSharedTokens();
#endif
        }
        catch
        {
            // Handle storage exceptions
        }
        return Task.CompletedTask;
    }
}
