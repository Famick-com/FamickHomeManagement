using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Platform-specific service for syncing calendar events to/from the device's native Calendar app.
/// Creates a "Famick" calendar on the device for bidirectional sync.
/// </summary>
public interface ICalendarSyncService
{
    /// <summary>
    /// Requests calendar read/write permission from the user.
    /// </summary>
    Task<bool> RequestPermissionAsync();

    /// <summary>
    /// Checks if the app has calendar read/write permission.
    /// </summary>
    Task<bool> HasPermissionAsync();

    /// <summary>
    /// Syncs the provided events to the device under a "Famick" calendar.
    /// Creates, updates, or deletes device events to match the server state.
    /// </summary>
    Task<CalendarSyncResult> SyncEventsToDeviceAsync(List<CalendarOccurrence> events, CancellationToken ct = default);

    /// <summary>
    /// Syncs a single event to the device (create or update). Does not delete other events.
    /// </summary>
    Task<bool> SyncSingleEventToDeviceAsync(CalendarOccurrence evt);

    /// <summary>
    /// Deletes a single synced event from the device and removes the mapping.
    /// </summary>
    Task<bool> DeleteSingleEventFromDeviceAsync(Guid serverEventId);

    /// <summary>
    /// Removes all Famick-synced events from the device.
    /// </summary>
    Task<CalendarSyncResult> RemoveAllSyncedEventsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current sync status (count, last sync time, permission state).
    /// </summary>
    Task<CalendarSyncStatus> GetSyncStatusAsync();

    /// <summary>
    /// Reads all events from the device's Famick calendar within the given date range.
    /// Used during the pull phase to detect local additions, edits, and deletions.
    /// </summary>
    Task<List<DeviceCalendarEventData>> ReadFamickCalendarEventsAsync(DateTime startUtc, DateTime endUtc);
}
