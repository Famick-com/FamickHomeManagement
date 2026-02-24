namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Platform-specific storage for navigation menu preferences.
/// Web uses localStorage, MAUI uses Preferences.
/// Stores which navigation groups are expanded.
/// </summary>
public interface INavMenuPreferenceStorage
{
    /// <summary>
    /// Gets the set of expanded navigation group names.
    /// </summary>
    /// <returns>A set of group names that should be expanded, or empty set if none</returns>
    Task<HashSet<string>> GetExpandedGroupsAsync();

    /// <summary>
    /// Saves the set of expanded navigation group names.
    /// </summary>
    /// <param name="expandedGroups">The set of group names that are expanded</param>
    Task SetExpandedGroupsAsync(HashSet<string> expandedGroups);

    /// <summary>
    /// Toggles a single group's expanded state and persists the change.
    /// </summary>
    /// <param name="groupName">The name of the group to toggle</param>
    /// <param name="isExpanded">Whether the group should be expanded</param>
    Task ToggleGroupAsync(string groupName, bool isExpanded);
}
