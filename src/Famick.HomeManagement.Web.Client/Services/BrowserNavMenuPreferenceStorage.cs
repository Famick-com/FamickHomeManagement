using System.Text.Json;
using Famick.HomeManagement.UI.Services;
using Microsoft.JSInterop;

namespace Famick.HomeManagement.Web.Client.Services;

/// <summary>
/// Browser-based navigation menu preference storage using localStorage.
/// Stores which navigation groups are expanded.
/// </summary>
public class BrowserNavMenuPreferenceStorage : INavMenuPreferenceStorage
{
    private readonly IJSRuntime _jsRuntime;
    private const string ExpandedGroupsKey = "nav_expanded_groups";

    // Cache to avoid repeated localStorage reads for toggle operations
    private HashSet<string>? _cachedGroups;

    public BrowserNavMenuPreferenceStorage(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task<HashSet<string>> GetExpandedGroupsAsync()
    {
        if (_cachedGroups != null)
        {
            return _cachedGroups;
        }

        try
        {
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", ExpandedGroupsKey);

            if (!string.IsNullOrEmpty(json))
            {
                var groups = JsonSerializer.Deserialize<List<string>>(json);
                _cachedGroups = groups != null ? new HashSet<string>(groups) : new HashSet<string>();
            }
            else
            {
                _cachedGroups = new HashSet<string>();
            }
        }
        catch
        {
            _cachedGroups = new HashSet<string>();
        }

        return _cachedGroups;
    }

    public async Task SetExpandedGroupsAsync(HashSet<string> expandedGroups)
    {
        try
        {
            _cachedGroups = expandedGroups;
            var json = JsonSerializer.Serialize(expandedGroups.ToList());
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", ExpandedGroupsKey, json);
        }
        catch
        {
            // Ignore storage errors (e.g., private browsing mode)
        }
    }

    public async Task ToggleGroupAsync(string groupName, bool isExpanded)
    {
        var groups = await GetExpandedGroupsAsync();

        if (isExpanded)
        {
            groups.Add(groupName);
        }
        else
        {
            groups.Remove(groupName);
        }

        await SetExpandedGroupsAsync(groups);
    }
}
