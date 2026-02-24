namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Service for managing inventory session state (selected location, etc.).
/// Persists across page refreshes using platform-specific storage.
/// </summary>
public interface IInventorySessionService
{
    /// <summary>
    /// Gets the currently selected location ID for the inventory session.
    /// </summary>
    Task<Guid?> GetSelectedLocationIdAsync();

    /// <summary>
    /// Sets the selected location ID for the inventory session.
    /// </summary>
    Task SetSelectedLocationIdAsync(Guid locationId);

    /// <summary>
    /// Clears the selected location.
    /// </summary>
    Task ClearSelectedLocationAsync();

    /// <summary>
    /// Gets the last scanned/searched query for quick re-scan.
    /// </summary>
    Task<string?> GetLastQueryAsync();

    /// <summary>
    /// Sets the last scanned/searched query.
    /// </summary>
    Task SetLastQueryAsync(string query);
}
