using Famick.HomeManagement.UI.Localization;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Browser-based language preference storage using localStorage.
/// Follows the same pattern as BrowserTokenStorage.
/// </summary>
public class BrowserLanguagePreferenceStorage : ILanguagePreferenceStorage
{
    private readonly IJSRuntime _jsRuntime;
    private const string LanguagePreferenceKey = "preferred_language";

    public BrowserLanguagePreferenceStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<string?> GetLanguagePreferenceAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LanguagePreferenceKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetLanguagePreferenceAsync(string languageCode)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LanguagePreferenceKey, languageCode);
        }
        catch
        {
            // Ignore storage errors (e.g., private browsing mode)
        }
    }

    public async Task ClearLanguagePreferenceAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LanguagePreferenceKey);
        }
        catch
        {
            // Ignore storage errors
        }
    }
}
