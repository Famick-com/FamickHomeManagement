using Famick.HomeManagement.UI.Services;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Browser-based shopping list preference storage using localStorage.
/// Stores last used shopping list per store.
/// </summary>
public class BrowserShoppingListPreferenceStorage : IShoppingListPreferenceStorage
{
    private readonly IJSRuntime _jsRuntime;
    private const string LastUsedStoreKey = "shopping_last_store";
    private const string LastUsedListKeyPrefix = "shopping_last_list_";

    public BrowserShoppingListPreferenceStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<Guid?> GetLastUsedListIdAsync(Guid shoppingLocationId)
    {
        try
        {
            var key = LastUsedListKeyPrefix + shoppingLocationId.ToString("N");
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);

            if (Guid.TryParse(value, out var listId))
            {
                return listId;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetLastUsedListIdAsync(Guid shoppingLocationId, Guid shoppingListId)
    {
        try
        {
            var key = LastUsedListKeyPrefix + shoppingLocationId.ToString("N");
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, shoppingListId.ToString());
        }
        catch
        {
            // Ignore storage errors (e.g., private browsing mode)
        }
    }

    public async Task<Guid?> GetLastUsedStoreIdAsync()
    {
        try
        {
            var value = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", LastUsedStoreKey);

            if (Guid.TryParse(value, out var storeId))
            {
                return storeId;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task SetLastUsedStoreIdAsync(Guid shoppingLocationId)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", LastUsedStoreKey, shoppingLocationId.ToString());
        }
        catch
        {
            // Ignore storage errors
        }
    }

    public async Task ClearPreferencesAsync()
    {
        try
        {
            // Clear last used store
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", LastUsedStoreKey);

            // Note: We don't clear individual list preferences as localStorage doesn't
            // provide a way to enumerate keys efficiently. They're harmless if orphaned.
        }
        catch
        {
            // Ignore storage errors
        }
    }
}
