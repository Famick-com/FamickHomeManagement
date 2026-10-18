namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Where the web app keeps its session tokens.
/// </summary>
/// <remarks>
/// The access token is held in memory only. The refresh token is an HttpOnly cookie the server
/// sets and reads (<c>RefreshTokenCookie</c>), so the app never sees it and nothing on the page
/// can read it. <see cref="GetRefreshTokenAsync"/> returns only a token left in localStorage by an
/// older build, so that session can be moved to the cookie once instead of being signed out.
/// </remarks>
public interface ITokenStorage
{
    Task<string?> GetAccessTokenAsync();

    /// <summary>A refresh token left in storage by an older build, or null.</summary>
    Task<string?> GetRefreshTokenAsync();

    /// <summary>
    /// Stores the access token. <paramref name="refreshToken"/> is normally null (the server keeps
    /// it in the cookie) and is never persisted.
    /// </summary>
    Task SetTokensAsync(string accessToken, string? refreshToken);

    /// <summary>
    /// Phase 2.5 — swap only the access token without rotating the refresh
    /// token. Used by the step-up reauth flow which returns a fresh access
    /// token (with refreshed <c>auth_time</c>) while preserving the existing
    /// refresh-token family.
    /// </summary>
    Task SetAccessTokenAsync(string accessToken);

    Task ClearTokensAsync();
}
