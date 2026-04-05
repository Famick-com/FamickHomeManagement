using System.Globalization;
using Famick.HomeManagement.Mobile.Models;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Coordinates the full calendar sync flow: push device edits to server,
/// fetch from server, sync to device, update mappings.
/// Mirrors the ContactSyncOrchestrator pattern.
/// </summary>
public class CalendarSyncOrchestrator
{
    private readonly ICalendarSyncService _syncService;
    private readonly ShoppingApiClient _apiClient;
    private readonly CalendarSyncMappingStore _mappingStore;

    /// <summary>
    /// Sync window: 30 days in the past.
    /// </summary>
    private const int SyncDaysPast = 30;

    /// <summary>
    /// Sync window: 90 days in the future.
    /// </summary>
    private const int SyncDaysFuture = 90;

    public CalendarSyncOrchestrator(
        ICalendarSyncService syncService,
        ShoppingApiClient apiClient,
        CalendarSyncMappingStore mappingStore)
    {
        _syncService = syncService;
        _apiClient = apiClient;
        _mappingStore = mappingStore;
    }

    /// <summary>
    /// Runs a full bidirectional sync:
    /// 1. Pull phase: detect device-side changes and push to server
    /// 2. Push phase: fetch server events and sync to device
    /// </summary>
    public async Task<CalendarSyncResult> SyncAsync(CancellationToken ct = default)
    {
        var hasPermission = await _syncService.HasPermissionAsync();
        if (!hasPermission)
            return CalendarSyncResult.Fail("Calendar permission not granted");

        var now = DateTime.UtcNow;
        var syncStart = now.AddDays(-SyncDaysPast);
        var syncEnd = now.AddDays(SyncDaysFuture);

        // Phase 0: Push device edits to server before pulling server state
        await PushDeviceEditsToServerAsync(syncStart, syncEnd, ct);

        // Phase 1: Fetch events from server (excluding external events -- only Famick events)
        var result = await _apiClient.GetCalendarEventsAsync(syncStart, syncEnd, includeExternal: false);
        if (!result.Success || result.Data == null)
            return CalendarSyncResult.Fail(result.ErrorMessage ?? "Failed to fetch calendar events from server");

        ct.ThrowIfCancellationRequested();

        // Phase 2: Sync to device
        var syncResult = await _syncService.SyncEventsToDeviceAsync(result.Data, ct);

        if (syncResult.Success)
        {
            Preferences.Set("CalendarSyncLastSyncedAt", DateTime.UtcNow.ToString("O"));
            Preferences.Set("CalendarSyncCount", _mappingStore.SyncedCount);
        }

        return syncResult;
    }

    /// <summary>
    /// Syncs a single event to the device immediately (e.g. after create/edit).
    /// Runs fire-and-forget -- failures are logged but not surfaced to the user.
    /// </summary>
    public async Task SyncSingleEventAsync(Guid eventId)
    {
        try
        {
            if (!IsSyncEnabled) return;

            var hasPermission = await _syncService.HasPermissionAsync();
            if (!hasPermission) return;

            var detailResult = await _apiClient.GetCalendarEventAsync(eventId);
            if (!detailResult.Success || detailResult.Data == null) return;

            var detail = detailResult.Data;
            var occurrence = new CalendarOccurrence
            {
                EventId = detail.Id,
                Title = detail.Title,
                Description = detail.Description,
                Location = detail.Location,
                StartTimeUtc = detail.StartTimeUtc,
                EndTimeUtc = detail.EndTimeUtc,
                IsAllDay = detail.IsAllDay,
                Color = detail.Color
            };

            await _syncService.SyncSingleEventToDeviceAsync(occurrence);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Deletes a single event from the device (e.g. when deleted in Famick).
    /// </summary>
    public async Task DeleteSingleEventAsync(Guid eventId)
    {
        try
        {
            if (!IsSyncEnabled) return;
            var hasPermission = await _syncService.HasPermissionAsync();
            if (!hasPermission) return;
            await _syncService.DeleteSingleEventFromDeviceAsync(eventId);
        }
        catch { /* Non-critical */ }
    }

    /// <summary>
    /// Removes all Famick-synced events from the device and clears mappings.
    /// </summary>
    public async Task<CalendarSyncResult> RemoveAllAsync(CancellationToken ct = default)
    {
        var result = await _syncService.RemoveAllSyncedEventsAsync(ct);

        if (result.Success)
        {
            Preferences.Remove("CalendarSyncLastSyncedAt");
            Preferences.Set("CalendarSyncCount", 0);
        }

        return result;
    }

    /// <summary>
    /// Gets the current sync status.
    /// </summary>
    public async Task<CalendarSyncStatus> GetStatusAsync()
    {
        return await _syncService.GetSyncStatusAsync();
    }

    /// <summary>
    /// Whether calendar sync is enabled by the user.
    /// </summary>
    public static bool IsSyncEnabled
    {
        get => Preferences.Get("CalendarSyncEnabled", false);
        set => Preferences.Set("CalendarSyncEnabled", value);
    }

    /// <summary>
    /// When the last successful sync completed.
    /// </summary>
    public static DateTime? LastSyncedAt
    {
        get
        {
            var str = Preferences.Get("CalendarSyncLastSyncedAt", null as string);
            return str != null ? DateTime.Parse(str, null, DateTimeStyles.RoundtripKind) : null;
        }
    }

    /// <summary>
    /// Whether a sync should run based on the minimum interval and enabled state.
    /// </summary>
    public static bool ShouldSync(TimeSpan minInterval)
    {
        if (!IsSyncEnabled) return false;
        var last = LastSyncedAt;
        return last == null || DateTime.UtcNow - last.Value > minInterval;
    }

    /// <summary>
    /// Pushes device-side edits to the server before the normal server->device sync.
    /// Reads the Famick calendar from the device, compares hashes to detect:
    /// - New events (on device but not in mapping) -> create on server
    /// - Edited events (device hash changed, server hash unchanged) -> update on server
    /// - Deleted events (in mapping but no longer on device) -> delete on server
    /// If both sides changed, server wins (skip device push).
    /// </summary>
    private async Task PushDeviceEditsToServerAsync(DateTime syncStart, DateTime syncEnd, CancellationToken ct)
    {
        try
        {
            var deviceEvents = await _syncService.ReadFamickCalendarEventsAsync(syncStart, syncEnd);
            var deviceToServer = _mappingStore.GetDeviceToServerMap();

            // Fetch household members for attendee matching
            List<HouseholdMember>? householdMembers = null;
            var hasAttendees = deviceEvents.Any(e => e.Attendees.Count > 0);
            if (hasAttendees)
            {
                var membersResult = await _apiClient.GetCalendarMembersAsync();
                if (membersResult.Success)
                    householdMembers = membersResult.Data;
            }

            // Build a set of device event IDs currently on device
            var deviceEventIds = new HashSet<string>(deviceEvents.Select(e => e.DeviceEventId));

            // Detect deletions: mapped events no longer on device
            var allSyncedIds = _mappingStore.GetAllSyncedServerEventIds();
            foreach (var serverId in allSyncedIds)
            {
                ct.ThrowIfCancellationRequested();

                var deviceId = _mappingStore.GetDeviceEventId(serverId);
                if (deviceId != null && !deviceEventIds.Contains(deviceId))
                {
                    // Event was deleted on device -> delete on server
                    try
                    {
                        await _apiClient.DeleteCalendarEventAsync(serverId);
                        _mappingStore.RemoveMapping(serverId);
                    }
                    catch { /* Non-critical */ }
                }
            }

            // Detect new events and edits
            foreach (var deviceEvent in deviceEvents)
            {
                ct.ThrowIfCancellationRequested();

                // Resolve attendees to household members
                var (members, externalAttendeeNotes) = ResolveAttendees(deviceEvent.Attendees, householdMembers);

                if (deviceToServer.TryGetValue(deviceEvent.DeviceEventId, out var serverId))
                {
                    // Existing mapped event -- check for device-side edits
                    var lastDeviceHash = _mappingStore.GetLastDeviceHash(serverId);
                    var currentDeviceHash = CalendarSyncMappingStore.ComputeDeviceEventHash(deviceEvent);

                    if (currentDeviceHash == lastDeviceHash)
                        continue; // No change on device

                    // Device was edited. Check if server also changed (server-wins conflict).
                    var lastSyncedHash = _mappingStore.GetLastSyncedHash(serverId);
                    var serverResult = await _apiClient.GetCalendarEventAsync(serverId);
                    if (!serverResult.Success || serverResult.Data == null) continue;

                    var serverOcc = new CalendarOccurrence
                    {
                        EventId = serverResult.Data.Id,
                        Title = serverResult.Data.Title,
                        Description = serverResult.Data.Description,
                        Location = serverResult.Data.Location,
                        StartTimeUtc = serverResult.Data.StartTimeUtc,
                        EndTimeUtc = serverResult.Data.EndTimeUtc,
                        IsAllDay = serverResult.Data.IsAllDay
                    };
                    var currentServerHash = CalendarSyncMappingStore.ComputeOccurrenceHash(serverOcc);

                    if (currentServerHash != lastSyncedHash)
                    {
                        // Both sides changed -> server wins, skip push
                        continue;
                    }

                    // Only device changed -> push to server
                    var updateRequest = new UpdateCalendarEventMobileRequest
                    {
                        Title = deviceEvent.Title,
                        Description = AppendExternalAttendeeNotes(deviceEvent.Description, externalAttendeeNotes),
                        Location = deviceEvent.Location,
                        StartTimeUtc = deviceEvent.StartTimeUtc,
                        EndTimeUtc = deviceEvent.EndTimeUtc,
                        IsAllDay = deviceEvent.IsAllDay,
                        RecurrenceRule = deviceEvent.RecurrenceRule,
                        Members = members,
                        EditScope = 3 // EntireSeries
                    };
                    await _apiClient.UpdateCalendarEventAsync(serverId, updateRequest);
                }
                else
                {
                    // New event created on device -> create on server
                    var createRequest = new CreateCalendarEventMobileRequest
                    {
                        Title = deviceEvent.Title,
                        Description = AppendExternalAttendeeNotes(deviceEvent.Description, externalAttendeeNotes),
                        Location = deviceEvent.Location,
                        StartTimeUtc = deviceEvent.StartTimeUtc,
                        EndTimeUtc = deviceEvent.EndTimeUtc,
                        IsAllDay = deviceEvent.IsAllDay,
                        RecurrenceRule = deviceEvent.RecurrenceRule,
                        Members = members
                    };

                    var createResult = await _apiClient.CreateCalendarEventAsync(createRequest);
                    if (createResult.Success && createResult.Data != null)
                    {
                        // Map the new server event to the existing device event
                        var occ = new CalendarOccurrence
                        {
                            EventId = createResult.Data.Id,
                            Title = createResult.Data.Title,
                            Description = createResult.Data.Description,
                            Location = createResult.Data.Location,
                            StartTimeUtc = createResult.Data.StartTimeUtc,
                            EndTimeUtc = createResult.Data.EndTimeUtc,
                            IsAllDay = createResult.Data.IsAllDay
                        };
                        var syncedHash = CalendarSyncMappingStore.ComputeOccurrenceHash(occ);
                        var devHash = CalendarSyncMappingStore.ComputeDeviceEventHash(deviceEvent);
                        _mappingStore.SetMapping(createResult.Data.Id, deviceEvent.DeviceEventId, syncedHash, devHash);
                    }
                }
            }

            _mappingStore.Save();
        }
        catch
        {
            // Non-critical -- continue with push phase even if pull fails
        }
    }

    /// <summary>
    /// Resolves device calendar attendees against household members.
    /// - Attendees matching a household member by email (primary) or display name (fallback)
    ///   are added as Members: required attendees -> Involved (1), optional -> Aware (2).
    /// - Non-matching attendees are returned as a notes string to append to the description.
    ///
    /// NOTE: This same resolution logic should be applied server-side when syncing ICS
    /// subscription feeds. See the TODO in ExternalCalendarService.SyncSingleSubscriptionAsync()
    /// for implementation details. The ICS equivalent fields are:
    ///   Attendee.Value (mailto: URI) -> Email, Attendee.CommonName -> Name,
    ///   Attendee.Role (REQ-PARTICIPANT/CHAIR -> Required, OPT-PARTICIPANT -> Optional).
    /// </summary>
    private static (List<CalendarMemberRequest> Members, string? ExternalAttendeeNotes) ResolveAttendees(
        List<DeviceCalendarAttendee> attendees,
        List<HouseholdMember>? householdMembers)
    {
        var members = new List<CalendarMemberRequest>();
        var externalAttendees = new List<string>();

        if (attendees.Count == 0)
            return (members, null);

        foreach (var attendee in attendees)
        {
            var matched = MatchHouseholdMember(attendee, householdMembers);

            if (matched != null)
            {
                // Don't add duplicate members
                if (members.Any(m => m.UserId == matched.Id))
                    continue;

                members.Add(new CalendarMemberRequest
                {
                    UserId = matched.Id,
                    ParticipationType = attendee.IsRequired ? 1 : 2 // 1=Involved, 2=Aware
                });
            }
            else
            {
                // External attendee -- collect for notes
                var displayText = !string.IsNullOrEmpty(attendee.Name) ? attendee.Name : attendee.Email;
                if (!string.IsNullOrEmpty(displayText))
                {
                    var role = attendee.IsRequired ? "required" : "optional";
                    externalAttendees.Add($"{displayText} ({role})");
                }
            }
        }

        var notes = externalAttendees.Count > 0
            ? "External attendees: " + string.Join(", ", externalAttendees)
            : null;

        return (members, notes);
    }

    /// <summary>
    /// Matches a device calendar attendee to a household member.
    /// Matches by email first (case-insensitive), then falls back to display name.
    /// </summary>
    private static HouseholdMember? MatchHouseholdMember(
        DeviceCalendarAttendee attendee,
        List<HouseholdMember>? householdMembers)
    {
        if (householdMembers == null || householdMembers.Count == 0)
            return null;

        // Match by email (most reliable)
        if (!string.IsNullOrEmpty(attendee.Email))
        {
            var byEmail = householdMembers.FirstOrDefault(m =>
                !string.IsNullOrEmpty(m.Email) &&
                string.Equals(m.Email, attendee.Email, StringComparison.OrdinalIgnoreCase));
            if (byEmail != null)
                return byEmail;
        }

        // Fallback: match by display name (case-insensitive)
        if (!string.IsNullOrEmpty(attendee.Name))
        {
            var byName = householdMembers.FirstOrDefault(m =>
                string.Equals(m.DisplayName, attendee.Name, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName;
        }

        return null;
    }

    /// <summary>
    /// Appends external attendee notes to the event description, if any.
    /// Preserves existing description content.
    /// </summary>
    private static string? AppendExternalAttendeeNotes(string? description, string? externalAttendeeNotes)
    {
        if (string.IsNullOrEmpty(externalAttendeeNotes))
            return description;

        if (string.IsNullOrEmpty(description))
            return externalAttendeeNotes;

        return description + "\n\n" + externalAttendeeNotes;
    }
}
