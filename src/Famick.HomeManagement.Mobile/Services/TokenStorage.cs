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

    /// <summary>
    /// Checks if the stored access token contains a must_change_password claim.
    /// Decodes the JWT payload without validation (just base64).
    /// </summary>
    public bool HasMustChangePasswordClaim()
    {
        try
        {
            var token = GetAccessToken();
            if (string.IsNullOrEmpty(token)) return false;

            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            // Decode the payload (second part), adding padding if needed
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            return json.Contains("\"must_change_password\"");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if the stored access token contains a must_accept_terms claim.
    /// </summary>
    public bool HasMustAcceptTermsClaim()
    {
        try
        {
            var token = GetAccessToken();
            if (string.IsNullOrEmpty(token)) return false;

            var parts = token.Split('.');
            if (parts.Length != 3) return false;

            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            return json.Contains("\"must_accept_terms\"");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts the email claim from the stored JWT access token.
    /// </summary>
    public string? GetEmailFromToken()
    {
        try
        {
            var token = GetAccessToken();
            if (string.IsNullOrEmpty(token)) return null;

            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            // Parse email from JWT claims — look for "email":"value" pattern
            var emailMatch = System.Text.RegularExpressions.Regex.Match(json, "\"email\"\\s*:\\s*\"([^\"]+)\"");
            return emailMatch.Success ? emailMatch.Groups[1].Value : null;
        }
        catch
        {
            return null;
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
