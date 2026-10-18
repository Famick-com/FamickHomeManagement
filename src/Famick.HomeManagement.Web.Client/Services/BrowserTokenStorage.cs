using Famick.HomeManagement.UI.Services;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Keeps the access token in memory. The refresh token is an HttpOnly cookie the browser holds
/// for the server; see <see cref="ITokenStorage"/>.
/// </summary>
/// <remarks>
/// Builds before the cookie kept both tokens in localStorage under <see cref="LegacyAccessTokenKey"/>
/// and <see cref="LegacyRefreshTokenKey"/>. The refresh token found there is offered once, through
/// <see cref="GetRefreshTokenAsync"/>, so the first refresh moves that session into the cookie;
/// storing the result, or clearing, deletes both keys.
/// </remarks>
public class BrowserTokenStorage : ITokenStorage
{
    private const string LegacyAccessTokenKey = "access_token";
    private const string LegacyRefreshTokenKey = "refresh_token";

    private readonly IJSRuntime _jsRuntime;
    private string? _accessToken;

    public BrowserTokenStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public Task<string?> GetAccessTokenAsync() => Task.FromResult(_accessToken);

    public async Task<string?> GetRefreshTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LegacyRefreshTokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetTokensAsync(string accessToken, string? refreshToken)
    {
        _accessToken = accessToken;
        await RemoveLegacyTokensAsync();
    }

    public Task SetAccessTokenAsync(string accessToken)
    {
        _accessToken = accessToken;
        return Task.CompletedTask;
    }

    public async Task ClearTokensAsync()
    {
        _accessToken = null;
        await RemoveLegacyTokensAsync();
    }

    private async Task RemoveLegacyTokensAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LegacyAccessTokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LegacyRefreshTokenKey);
        }
        catch
        {
            // Storage unavailable (private browsing); nothing was left there to remove.
        }
    }
}
