using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Custom authentication state provider that reads JWT tokens from storage.
/// Proactively refreshes expired access tokens using the refresh token.
/// </summary>
public class ApiAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IApiClient _apiClient;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public ApiAuthStateProvider(ITokenStorage tokenStorage, IApiClient apiClient)
    {
        _tokenStorage = tokenStorage;
        _apiClient = apiClient;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _tokenStorage.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(_anonymous);
            }

            var claims = ParseClaimsFromJwt(token);
            if (claims == null || !claims.Any())
            {
                return new AuthenticationState(_anonymous);
            }

            // Check if token is expired
            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            if (expClaim != null && long.TryParse(expClaim.Value, out var exp))
            {
                var expDate = DateTimeOffset.FromUnixTimeSeconds(exp);
                if (expDate <= DateTimeOffset.UtcNow)
                {
                    // Access token expired - try to refresh using refresh token
                    var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
                    if (!string.IsNullOrEmpty(refreshToken))
                    {
                        var result = await _apiClient.RefreshTokenAsync(refreshToken);
                        if (result.IsSuccess)
                        {
                            // Re-read the new access token (already saved by HttpApiClient)
                            token = await _tokenStorage.GetAccessTokenAsync();
                            if (!string.IsNullOrEmpty(token))
                            {
                                claims = ParseClaimsFromJwt(token);
                                if (claims != null && claims.Any())
                                {
                                    var refreshedIdentity = new ClaimsIdentity(claims, "jwt", "name", "role");
                                    return new AuthenticationState(new ClaimsPrincipal(refreshedIdentity));
                                }
                            }
                        }
                        // Refresh failed - clear invalid tokens
                        await _tokenStorage.ClearTokensAsync();
                    }
                    return new AuthenticationState(_anonymous);
                }
            }

            var identity = new ClaimsIdentity(claims, "jwt", "name", "role");
            var user = new ClaimsPrincipal(identity);
            return new AuthenticationState(user);
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    /// <summary>
    /// Notify that authentication state has changed.
    /// Call this after login/logout.
    /// </summary>
    public void NotifyAuthenticationStateChanged()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Parse claims from a JWT token without validation (client-side parsing).
    /// </summary>
    private static IEnumerable<Claim>? ParseClaimsFromJwt(string jwt)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims;
        }
        catch
        {
            return null;
        }
    }
}
