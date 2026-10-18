using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Core.Messaging.Messages;
using Microsoft.AspNetCore.Components.Authorization;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Authentication state from the in-memory access token. When there is none (a fresh page load)
/// or it has expired, the session cookie is exchanged for a new one.
/// </summary>
public class ApiAuthStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStorage _tokenStorage;
    private readonly IApiClient _apiClient;
    private readonly IMessageBus _messageBus;
    private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());

    public ApiAuthStateProvider(ITokenStorage tokenStorage, IApiClient apiClient, IMessageBus messageBus)
    {
        _tokenStorage = tokenStorage;
        _apiClient = apiClient;
        _messageBus = messageBus;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // The access token lives in memory, so after a page load there is none yet. The session
            // itself is the refresh cookie: ask for a fresh access token, as for an expired one.
            var token = await _tokenStorage.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token) || IsExpired(ParseClaimsFromJwt(token)))
            {
                if (!await _apiClient.RefreshSessionAsync())
                {
                    return new AuthenticationState(_anonymous);
                }

                token = await _tokenStorage.GetAccessTokenAsync();
            }

            var claims = string.IsNullOrEmpty(token) ? null : ParseClaimsFromJwt(token);
            if (claims == null || !claims.Any())
            {
                return new AuthenticationState(_anonymous);
            }

            var identity = new ClaimsIdentity(claims, "jwt", "name", "role");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(_anonymous);
        }
    }

    private static bool IsExpired(IEnumerable<Claim>? claims)
    {
        var exp = claims?.FirstOrDefault(c => c.Type == "exp")?.Value;
        return exp == null
            || !long.TryParse(exp, out var seconds)
            || DateTimeOffset.FromUnixTimeSeconds(seconds) <= DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Notify that authentication state has changed.
    /// Call this after login/logout.
    /// </summary>
    public async Task NotifyAuthenticationStateChangedAsync()
    {
        var isAuthenticated = await _tokenStorage.GetAccessTokenAsync() is not null;
        _messageBus.Publish(new AuthenticationStateChangedMessage(isAuthenticated) { Source = "blazor" });
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
