namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Platform-specific storage for shopping list preferences.
/// Web uses localStorage, MAUI uses Preferences.
/// Stores the last used shopping list ID per store.
/// </summary>
public interface IShoppingListPreferenceStorage
{
    /// <summary>
    /// Gets the last used shopping list ID for a specific store.
    /// </summary>
    /// <param name="shoppingLocationId">The store ID</param>
    /// <returns>The shopping list ID, or null if not set</returns>
    Task<Guid?> GetLastUsedListIdAsync(Guid shoppingLocationId);

    /// <summary>
    /// Sets the last used shopping list ID for a specific store.
    /// </summary>
    /// <param name="shoppingLocationId">The store ID</param>
    /// <param name="shoppingListId">The shopping list ID to remember</param>
    Task SetLastUsedListIdAsync(Guid shoppingLocationId, Guid shoppingListId);

    /// <summary>
    /// Gets the last used store ID (across all stores).
    /// </summary>
    /// <returns>The store ID, or null if not set</returns>
    Task<Guid?> GetLastUsedStoreIdAsync();

    /// <summary>
    /// Sets the last used store ID.
    /// </summary>
    /// <param name="shoppingLocationId">The store ID to remember</param>
    Task SetLastUsedStoreIdAsync(Guid shoppingLocationId);

    /// <summary>
    /// Clears all shopping list preferences.
    /// </summary>
    Task ClearPreferencesAsync();
}
