using Famick.HomeManagement.UI.Services;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Browser-based inventory session service using localStorage.
/// </summary>
public class BrowserInventorySessionService : IInventorySessionService
{
    private readonly IJSRuntime _jsRuntime;
    private const string SelectedLocationKey = "inventory_session_location";
    private const string LastQueryKey = "inventory_session_last_query";

    public BrowserInventorySessionService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<Guid?> GetSelectedLocationIdAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", SelectedLocationKey);
            if (Guid.TryParse(value, out var locationId))
            {
                return locationId;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetSelectedLocationIdAsync(Guid locationId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SelectedLocationKey, locationId.ToString());
        }
        catch
        {
            // Ignore storage errors (e.g., private browsing mode)
        }
    }

    public async Task ClearSelectedLocationAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", SelectedLocationKey);
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task<string?> GetLastQueryAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LastQueryKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetLastQueryAsync(string query)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LastQueryKey, query);
        }
        catch
        {
            // Ignore storage errors
        }
    }
}
