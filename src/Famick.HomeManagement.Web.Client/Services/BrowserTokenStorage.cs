using Famick.HomeManagement.UI.Services;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Browser-based token storage using localStorage.
/// </summary>
public class BrowserTokenStorage : ITokenStorage
{
    private readonly IJSRuntime _jsRuntime;
    private const string AccessTokenKey = "access_token";
    private const string RefreshTokenKey = "refresh_token";

    public BrowserTokenStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", AccessTokenKey);
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
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
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
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", AccessTokenKey, accessToken);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, refreshToken);
        }
        catch
        {
            // Ignore storage errors (e.g., private browsing mode)
        }
    }

    public async Task ClearTokensAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", AccessTokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
        }
        catch
        {
            // Ignore storage errors
        }
    }
}
