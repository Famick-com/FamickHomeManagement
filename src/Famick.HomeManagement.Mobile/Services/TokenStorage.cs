using System.Text.Json;

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
    /// Phase 2.5 — swap only the access token without rotating the refresh
    /// token. Used by the step-up reauth flow which receives a fresh access
    /// token (with updated <c>auth_time</c>) but no new refresh token.
    /// </summary>
    public async Task SetAccessTokenAsync(string accessToken)
    {
        try
        {
            await SecureStorage.Default.SetAsync(AccessTokenKey, accessToken).ConfigureAwait(false);
#if IOS
            // Keep the iOS shared keychain in sync so the widget extension sees
            // the rotated access token. The refresh token there is unchanged
            // — we re-read it and write both back via SetSharedTokens.
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(refreshToken))
            {
                Platforms.iOS.SharedKeychainService.SetSharedTokens(accessToken, refreshToken, _apiSettings.BaseUrl);
            }
#endif
        }
        catch
        {
            // Handle storage exceptions (e.g., secure storage not available)
        }
    }

    /// <summary>
    /// Decodes the payload of the stored access token. The signature is not checked — these
    /// claims only steer local navigation, and the server re-decides on every request.
    /// </summary>
    private JsonDocument? ReadAccessTokenPayload()
    {
        try
        {
            var token = GetAccessToken();
            if (string.IsNullOrEmpty(token)) return null;

            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            // base64url -> base64, restoring the padding JWT strips.
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            return JsonDocument.Parse(Convert.FromBase64String(payload));
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a gate claim, which is true only when the claim is present AND says so.
    /// <para>
    /// These were substring tests for the claim name, which report true for a claim whose
    /// value is "false". That is latent rather than live — <c>TokenService</c> only emits
    /// these claims when they are true — but a gate that cannot be turned off traps the user
    /// on the screen it guards, so it should not depend on the server never writing one.
    /// </para>
    /// </summary>
    private bool HasTrueClaim(string claimName)
    {
        using var document = ReadAccessTokenPayload();
        if (document == null) return false;

        if (!document.RootElement.TryGetProperty(claimName, out var claim)) return false;

        return claim.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            // TokenService writes Claim values as strings, so "true" is the usual shape.
            JsonValueKind.String => bool.TryParse(claim.GetString(), out var parsed) && parsed,
            _ => false
        };
    }

    /// <summary>
    /// Checks whether the stored access token requires a password change before the app is usable.
    /// </summary>
    public bool HasMustChangePasswordClaim() => HasTrueClaim("must_change_password");

    /// <summary>
    /// Checks whether the stored access token requires terms acceptance before the app is usable.
    /// </summary>
    public bool HasMustAcceptTermsClaim() => HasTrueClaim("must_accept_terms");

    /// <summary>
    /// Extracts the email claim from the stored JWT access token.
    /// </summary>
    public string? GetEmailFromToken()
    {
        using var document = ReadAccessTokenPayload();
        if (document == null) return null;

        if (!document.RootElement.TryGetProperty("email", out var email)) return null;

        return email.ValueKind == JsonValueKind.String ? email.GetString() : null;
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
