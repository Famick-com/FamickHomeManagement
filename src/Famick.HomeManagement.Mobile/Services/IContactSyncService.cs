using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Platform-specific service for syncing contacts to the device's native Contacts app.
/// </summary>
public interface IContactSyncService
{
    /// <summary>
    /// Requests contact read/write permission from the user.
    /// </summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Checks if the app has contact read/write permission.
    /// </summary>
    Task<bool> HasPermissionAsync();

    /// <summary>
    /// Syncs the provided contacts to the device under a "Famick" group.
    /// Creates, updates, or deletes device contacts to match the server state.
    /// </summary>
    Task<ContactSyncResult> SyncContactsAsync(List<ContactDetailDto> contacts, CancellationToken ct = default);

    /// <summary>
    /// Removes all Famick-synced contacts from the device.
    /// </summary>
    Task<ContactSyncResult> RemoveAllSyncedContactsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current sync status (count, last sync time, permission state).
    /// </summary>
    Task<ContactSyncStatus> GetSyncStatusAsync();
}
