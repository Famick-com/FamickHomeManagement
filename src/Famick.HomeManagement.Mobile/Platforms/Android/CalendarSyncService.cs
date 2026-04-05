using Android.Content;
using Android.Database;
using Android.Provider;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;
using Application = Android.App.Application;

namespace Famick.HomeManagement.Mobile.Platforms.Android;

/// <summary>
/// Android implementation of calendar sync using CalendarContract.
/// Syncs events into a "Famick" calendar on the device.
/// </summary>
public class CalendarSyncService : ICalendarSyncService
{
    private const string FamickCalendarName = "Famick";
    private const string FamickAccountName = "Famick";
    private const string FamickAccountType = "com.famick.homemanagement";
    private const string CalendarIdPrefKey = "FamickCalendarId";
    private const int FamickCalendarColor = unchecked((int)0xFF518751); // #518751
    private readonly CalendarSyncMappingStore _mappingStore;

    public CalendarSyncService(CalendarSyncMappingStore mappingStore)
    {
        _mappingStore = mappingStore;
    }

    public async Task<bool> RequestPermissionAsync()
    {
        var readStatus = await Permissions.RequestAsync<Permissions.CalendarRead>();
        var writeStatus = await Permissions.RequestAsync<Permissions.CalendarWrite>();
        return readStatus == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
    }

    public async Task<bool> HasPermissionAsync()
    {
        var readStatus = await Permissions.CheckStatusAsync<Permissions.CalendarRead>();
        var writeStatus = await Permissions.CheckStatusAsync<Permissions.CalendarWrite>();
        return readStatus == PermissionStatus.Granted && writeStatus == PermissionStatus.Granted;
    }

    public async Task<CalendarSyncResult> SyncEventsToDeviceAsync(
        List<CalendarOccurrence> events, CancellationToken ct = default)
    {
        try
        {
            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return CalendarSyncResult.Fail("ContentResolver not available");

            var calendarId = GetOrCreateFamickCalendar(resolver);
            if (calendarId < 0)
                return CalendarSyncResult.Fail("Failed to create Famick calendar");

            var serverEventIds = events.Select(e => e.EventId).ToHashSet();
            var created = 0;
            var updated = 0;
            var deleted = 0;
            var failed = 0;

            foreach (var evt in events)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var hash = CalendarSyncMappingStore.ComputeOccurrenceHash(evt);
                    var existingDeviceId = _mappingStore.GetDeviceEventId(evt.EventId);
                    var existingHash = _mappingStore.GetLastSyncedHash(evt.EventId);

                    if (existingDeviceId != null && hash == existingHash)
                        continue; // No changes

                    if (existingDeviceId != null)
                    {
                        if (UpdateDeviceEvent(resolver, existingDeviceId, evt, calendarId))
                        {
                            var deviceHash = ComputeDeviceHashFromOccurrence(evt);
                            _mappingStore.SetMapping(evt.EventId, existingDeviceId, hash, deviceHash);
                            updated++;
                        }
                        else
                        {
                            // Recreate if update failed
                            var deviceId = CreateDeviceEvent(resolver, evt, calendarId);
                            if (deviceId != null)
                            {
                                var deviceHash = ComputeDeviceHashFromOccurrence(evt);
                                _mappingStore.SetMapping(evt.EventId, deviceId, hash, deviceHash);
                                updated++;
                            }
                            else
                                failed++;
                        }
                    }
                    else
                    {
                        var deviceId = CreateDeviceEvent(resolver, evt, calendarId);
                        if (deviceId != null)
                        {
                            var deviceHash = ComputeDeviceHashFromOccurrence(evt);
                            _mappingStore.SetMapping(evt.EventId, deviceId, hash, deviceHash);
                            created++;
                        }
                        else
                            failed++;
                    }
                }
                catch
                {
                    failed++;
                }
            }

            // Delete events no longer on server
            var syncedIds = _mappingStore.GetAllSyncedServerEventIds();
            foreach (var syncedId in syncedIds)
            {
                if (!serverEventIds.Contains(syncedId))
                {
                    var deviceId = _mappingStore.GetDeviceEventId(syncedId);
                    if (deviceId != null && DeleteDeviceEvent(resolver, deviceId))
                    {
                        _mappingStore.RemoveMapping(syncedId);
                        deleted++;
                    }
                }
            }

            _mappingStore.Save();
            var result = CalendarSyncResult.Ok(created, updated, deleted);
            result.Failed = failed;
            return result;
        }
        catch (Exception ex)
        {
            return CalendarSyncResult.Fail($"Sync failed: {ex.Message}");
        }
    }

    public Task<bool> SyncSingleEventToDeviceAsync(CalendarOccurrence evt)
    {
        try
        {
            var resolver = Application.Context.ContentResolver;
            if (resolver == null) return Task.FromResult(false);

            var calendarId = GetOrCreateFamickCalendar(resolver);
            if (calendarId < 0) return Task.FromResult(false);

            var hash = CalendarSyncMappingStore.ComputeOccurrenceHash(evt);
            var deviceHash = ComputeDeviceHashFromOccurrence(evt);
            var existingDeviceId = _mappingStore.GetDeviceEventId(evt.EventId);

            if (existingDeviceId != null)
            {
                if (UpdateDeviceEvent(resolver, existingDeviceId, evt, calendarId))
                    _mappingStore.SetMapping(evt.EventId, existingDeviceId, hash, deviceHash);
                else
                {
                    var deviceId = CreateDeviceEvent(resolver, evt, calendarId);
                    if (deviceId != null)
                        _mappingStore.SetMapping(evt.EventId, deviceId, hash, deviceHash);
                    else
                        return Task.FromResult(false);
                }
            }
            else
            {
                var deviceId = CreateDeviceEvent(resolver, evt, calendarId);
                if (deviceId != null)
                    _mappingStore.SetMapping(evt.EventId, deviceId, hash, deviceHash);
                else
                    return Task.FromResult(false);
            }

            _mappingStore.Save();
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteSingleEventFromDeviceAsync(Guid serverEventId)
    {
        try
        {
            var deviceId = _mappingStore.GetDeviceEventId(serverEventId);
            if (deviceId == null) return Task.FromResult(false);

            var resolver = Application.Context.ContentResolver;
            if (resolver == null) return Task.FromResult(false);

            var deleted = DeleteDeviceEvent(resolver, deviceId);
            if (deleted)
            {
                _mappingStore.RemoveMapping(serverEventId);
                _mappingStore.Save();
            }
            return Task.FromResult(deleted);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<CalendarSyncResult> RemoveAllSyncedEventsAsync(CancellationToken ct = default)
    {
        try
        {
            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return Task.FromResult(CalendarSyncResult.Fail("ContentResolver not available"));

            var syncedIds = _mappingStore.GetAllSyncedServerEventIds();
            var deleted = 0;

            foreach (var syncedId in syncedIds)
            {
                ct.ThrowIfCancellationRequested();
                var deviceId = _mappingStore.GetDeviceEventId(syncedId);
                if (deviceId != null && DeleteDeviceEvent(resolver, deviceId))
                    deleted++;
            }

            // Remove the Famick calendar
            RemoveFamickCalendar(resolver);

            _mappingStore.Clear();
            return Task.FromResult(CalendarSyncResult.Ok(0, 0, deleted));
        }
        catch (Exception ex)
        {
            return Task.FromResult(CalendarSyncResult.Fail($"Remove failed: {ex.Message}"));
        }
    }

    public async Task<CalendarSyncStatus> GetSyncStatusAsync()
    {
        var hasPermission = await HasPermissionAsync();
        return new CalendarSyncStatus
        {
            SyncedCount = _mappingStore.SyncedCount,
            LastSyncedAt = _mappingStore.LastSyncedAt,
            HasPermission = hasPermission
        };
    }

    public Task<List<DeviceCalendarEventData>> ReadFamickCalendarEventsAsync(DateTime startUtc, DateTime endUtc)
    {
        var result = new List<DeviceCalendarEventData>();

        try
        {
            var resolver = Application.Context.ContentResolver;
            if (resolver == null)
                return Task.FromResult(result);

            var calendarId = Preferences.Get(CalendarIdPrefKey, -1L);
            if (calendarId < 0)
                return Task.FromResult(result);

            // Query events from the Famick calendar
            var startMillis = new DateTimeOffset(startUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();
            var endMillis = new DateTimeOffset(endUtc, TimeSpan.Zero).ToUnixTimeMilliseconds();

            var eventsUri = CalendarContract.Events.ContentUri;
            if (eventsUri == null)
                return Task.FromResult(result);

            using var cursor = resolver.Query(
                eventsUri,
                new[] { "_id", "title", "description", "eventLocation", "dtstart", "dtend", "allDay", "rrule" },
                $"calendar_id = ?",
                new[] { calendarId.ToString() },
                null);

            if (cursor == null)
                return Task.FromResult(result);

            while (cursor.MoveToNext())
            {
                var eventId = cursor.GetLong(0);
                var dtStart = cursor.GetLong(4);
                var dtEnd = cursor.GetLong(5);

                // Filter by date range
                if (dtStart > endMillis || dtEnd < startMillis)
                    continue;

                var eventData = new DeviceCalendarEventData
                {
                    DeviceEventId = eventId.ToString(),
                    Title = cursor.GetString(1) ?? "",
                    Description = cursor.IsNull(2) ? null : cursor.GetString(2),
                    Location = cursor.IsNull(3) ? null : cursor.GetString(3),
                    StartTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(dtStart).UtcDateTime,
                    EndTimeUtc = DateTimeOffset.FromUnixTimeMilliseconds(dtEnd).UtcDateTime,
                    IsAllDay = cursor.GetInt(6) == 1,
                    RecurrenceRule = cursor.IsNull(7) ? null : cursor.GetString(7)
                };

                // Read attendees for this event
                ReadAttendeesForEvent(resolver, eventId, eventData);

                result.Add(eventData);
            }
        }
        catch
        {
            // Return whatever we have
        }

        return Task.FromResult(result);
    }

    #region CalendarContract Helpers

    private long GetOrCreateFamickCalendar(ContentResolver resolver)
    {
        // Try stored ID
        var storedId = Preferences.Get(CalendarIdPrefKey, -1L);
        if (storedId > 0 && CalendarExists(resolver, storedId))
            return storedId;

        // Search by name
        var existingId = FindFamickCalendar(resolver);
        if (existingId > 0)
        {
            Preferences.Set(CalendarIdPrefKey, existingId);
            return existingId;
        }

        // Create new
        var values = new ContentValues();
        values.Put(CalendarContract.Calendars.InterfaceConsts.AccountName, FamickAccountName);
        values.Put(CalendarContract.Calendars.InterfaceConsts.AccountType, CalendarContract.AccountTypeLocal);
        values.Put(CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName, FamickCalendarName);
        values.Put(CalendarContract.Calendars.Name, FamickCalendarName);
        values.Put(CalendarContract.Calendars.InterfaceConsts.CalendarColor, FamickCalendarColor);
        values.Put(CalendarContract.Calendars.InterfaceConsts.CalendarAccessLevel, (int)CalendarAccess.AccessOwner);
        values.Put(CalendarContract.Calendars.InterfaceConsts.OwnerAccount, FamickAccountName);
        values.Put(CalendarContract.Calendars.InterfaceConsts.Visible, 1);
        values.Put(CalendarContract.Calendars.InterfaceConsts.SyncEvents, 1);

        var calUri = CalendarContract.Calendars.ContentUri?
            .BuildUpon()?
            .AppendQueryParameter(CalendarContract.CallerIsSyncadapter, "true")?
            .AppendQueryParameter(CalendarContract.Calendars.InterfaceConsts.AccountName, FamickAccountName)?
            .AppendQueryParameter(CalendarContract.Calendars.InterfaceConsts.AccountType, CalendarContract.AccountTypeLocal)?
            .Build();

        if (calUri == null) return -1;

        var uri = resolver.Insert(calUri, values);
        if (uri == null) return -1;

        var id = long.Parse(uri.LastPathSegment ?? "-1");
        if (id > 0)
            Preferences.Set(CalendarIdPrefKey, id);

        return id;
    }

    private static bool CalendarExists(ContentResolver resolver, long calendarId)
    {
        try
        {
            using var cursor = resolver.Query(
                CalendarContract.Calendars.ContentUri,
                new[] { "_id" },
                "_id = ?",
                new[] { calendarId.ToString() },
                null);
            return cursor != null && cursor.MoveToFirst();
        }
        catch
        {
            return false;
        }
    }

    private static long FindFamickCalendar(ContentResolver resolver)
    {
        try
        {
            using var cursor = resolver.Query(
                CalendarContract.Calendars.ContentUri,
                new[] { "_id", CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName },
                $"{CalendarContract.Calendars.InterfaceConsts.CalendarDisplayName} = ?",
                new[] { FamickCalendarName },
                null);

            if (cursor != null && cursor.MoveToFirst())
                return cursor.GetLong(0);
        }
        catch { }

        return -1;
    }

    private void RemoveFamickCalendar(ContentResolver resolver)
    {
        var calendarId = Preferences.Get(CalendarIdPrefKey, -1L);
        if (calendarId < 0) return;

        try
        {
            var calUri = ContentUris.WithAppendedId(CalendarContract.Calendars.ContentUri, calendarId)?
                .BuildUpon()?
                .AppendQueryParameter(CalendarContract.CallerIsSyncadapter, "true")?
                .AppendQueryParameter(CalendarContract.Calendars.InterfaceConsts.AccountName, FamickAccountName)?
                .AppendQueryParameter(CalendarContract.Calendars.InterfaceConsts.AccountType, CalendarContract.AccountTypeLocal)?
                .Build();

            if (calUri != null)
                resolver.Delete(calUri, null, null);
        }
        catch { }

        Preferences.Remove(CalendarIdPrefKey);
    }

    private static string? CreateDeviceEvent(ContentResolver resolver, CalendarOccurrence evt, long calendarId)
    {
        var values = BuildEventValues(evt, calendarId);

        var uri = resolver.Insert(CalendarContract.Events.ContentUri!, values);
        return uri?.LastPathSegment;
    }

    private static bool UpdateDeviceEvent(ContentResolver resolver, string deviceEventId, CalendarOccurrence evt, long calendarId)
    {
        if (!long.TryParse(deviceEventId, out var eventId))
            return false;

        var values = BuildEventValues(evt, calendarId);
        var eventUri = ContentUris.WithAppendedId(CalendarContract.Events.ContentUri, eventId);
        if (eventUri == null) return false;

        var rows = resolver.Update(eventUri, values, null, null);
        return rows > 0;
    }

    private static bool DeleteDeviceEvent(ContentResolver resolver, string deviceEventId)
    {
        if (!long.TryParse(deviceEventId, out var eventId))
            return false;

        var eventUri = ContentUris.WithAppendedId(CalendarContract.Events.ContentUri, eventId);
        if (eventUri == null) return true; // Already gone

        try
        {
            resolver.Delete(eventUri, null, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static ContentValues BuildEventValues(CalendarOccurrence evt, long calendarId)
    {
        var values = new ContentValues();
        values.Put(CalendarContract.Events.InterfaceConsts.CalendarId, calendarId);
        values.Put(CalendarContract.Events.InterfaceConsts.Title, evt.Title);
        values.Put(CalendarContract.Events.InterfaceConsts.Description, evt.Description);
        values.Put(CalendarContract.Events.InterfaceConsts.EventLocation, evt.Location);
        values.Put(CalendarContract.Events.InterfaceConsts.AllDay, evt.IsAllDay ? 1 : 0);
        values.Put(CalendarContract.Events.InterfaceConsts.EventTimezone, "UTC");

        if (evt.IsAllDay)
        {
            var startLocal = evt.StartTimeUtc.ToLocalTime().Date;
            var endLocal = evt.EndTimeUtc.ToLocalTime().Date;
            values.Put(CalendarContract.Events.InterfaceConsts.Dtstart,
                new DateTimeOffset(startLocal, TimeSpan.Zero).ToUnixTimeMilliseconds());
            values.Put(CalendarContract.Events.InterfaceConsts.Dtend,
                new DateTimeOffset(endLocal, TimeSpan.Zero).ToUnixTimeMilliseconds());
        }
        else
        {
            values.Put(CalendarContract.Events.InterfaceConsts.Dtstart,
                new DateTimeOffset(evt.StartTimeUtc, TimeSpan.Zero).ToUnixTimeMilliseconds());
            values.Put(CalendarContract.Events.InterfaceConsts.Dtend,
                new DateTimeOffset(evt.EndTimeUtc, TimeSpan.Zero).ToUnixTimeMilliseconds());
        }

        return values;
    }

    private static void ReadAttendeesForEvent(ContentResolver resolver, long eventId, DeviceCalendarEventData eventData)
    {
        try
        {
            var attendeesUri = CalendarContract.Attendees.ContentUri;
            if (attendeesUri == null) return;

            using var cursor = resolver.Query(
                attendeesUri,
                new[]
                {
                    CalendarContract.Attendees.InterfaceConsts.AttendeeName,
                    CalendarContract.Attendees.InterfaceConsts.AttendeeEmail,
                    CalendarContract.Attendees.InterfaceConsts.AttendeeType
                },
                $"{CalendarContract.Attendees.InterfaceConsts.EventId} = ?",
                new[] { eventId.ToString() },
                null);

            if (cursor == null) return;

            while (cursor.MoveToNext())
            {
                var name = cursor.IsNull(0) ? null : cursor.GetString(0);
                var email = cursor.IsNull(1) ? null : cursor.GetString(1);
                var type = cursor.GetInt(2); // 1=Required, 2=Optional

                eventData.Attendees.Add(new DeviceCalendarAttendee
                {
                    Name = name,
                    Email = email,
                    IsRequired = type == 1 // TYPE_REQUIRED
                });
            }
        }
        catch
        {
            // Non-critical -- event will sync without attendee data
        }
    }

    private static string ComputeDeviceHashFromOccurrence(CalendarOccurrence evt)
    {
        var deviceData = new DeviceCalendarEventData
        {
            Title = evt.Title,
            Description = evt.Description,
            Location = evt.Location,
            StartTimeUtc = evt.StartTimeUtc,
            EndTimeUtc = evt.EndTimeUtc,
            IsAllDay = evt.IsAllDay
        };
        return CalendarSyncMappingStore.ComputeDeviceEventHash(deviceData);
    }

    #endregion
}
