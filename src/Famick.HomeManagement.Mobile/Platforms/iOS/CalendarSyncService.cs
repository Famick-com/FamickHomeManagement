using EventKit;
using Foundation;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Platforms.iOS;

/// <summary>
/// iOS implementation of calendar sync using EventKit (EKEventStore).
/// Syncs events into a "Famick" calendar on the device.
/// </summary>
public class CalendarSyncService : ICalendarSyncService
{
    private const string FamickCalendarTitle = "Famick";
    private const string CalendarIdPrefKey = "FamickCalendarIdentifier";
    private readonly CalendarSyncMappingStore _mappingStore;

    public CalendarSyncService(CalendarSyncMappingStore mappingStore)
    {
        _mappingStore = mappingStore;
    }

    public Task<bool> RequestPermissionAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var store = new EKEventStore();

        // RequestAccess works on all iOS versions including 17+.
        // On iOS 17+ it still prompts for full access to events.
        store.RequestAccess(EKEntityType.Event, (granted, error) =>
        {
            tcs.SetResult(granted);
        });

        return tcs.Task;
    }

    public Task<bool> HasPermissionAsync()
    {
        var status = EKEventStore.GetAuthorizationStatus(EKEntityType.Event);
        // iOS < 17: Authorized (2) after granting access
        // iOS 17+:  FullAccess (3) after granting full access, but FullAccess is not
        //           available in the .NET MAUI EventKit bindings. Cast to int to handle both.
        var statusInt = (int)status;
        var granted = statusInt == (int)EKAuthorizationStatus.Authorized || statusInt == 3;
        return Task.FromResult(granted);
    }

    public async Task<CalendarSyncResult> SyncEventsToDeviceAsync(
        List<CalendarOccurrence> events, CancellationToken ct = default)
    {
        try
        {
            var store = new EKEventStore();
            var calendar = GetOrCreateFamickCalendar(store);
            if (calendar == null)
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
                        // Update existing
                        if (UpdateDeviceEvent(store, existingDeviceId, evt, calendar))
                        {
                            var deviceHash = ComputeDeviceHashFromOccurrence(evt);
                            _mappingStore.SetMapping(evt.EventId, existingDeviceId, hash, deviceHash);
                            updated++;
                        }
                        else
                        {
                            // Event may have been deleted on device; recreate
                            var deviceId = CreateDeviceEvent(store, evt, calendar);
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
                        var deviceId = CreateDeviceEvent(store, evt, calendar);
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

            // Delete events that are no longer on the server
            var syncedIds = _mappingStore.GetAllSyncedServerEventIds();
            foreach (var syncedId in syncedIds)
            {
                if (!serverEventIds.Contains(syncedId))
                {
                    var deviceId = _mappingStore.GetDeviceEventId(syncedId);
                    if (deviceId != null && DeleteDeviceEvent(store, deviceId))
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
            var store = new EKEventStore();
            var calendar = GetOrCreateFamickCalendar(store);
            if (calendar == null) return Task.FromResult(false);

            var hash = CalendarSyncMappingStore.ComputeOccurrenceHash(evt);
            var deviceHash = ComputeDeviceHashFromOccurrence(evt);
            var existingDeviceId = _mappingStore.GetDeviceEventId(evt.EventId);

            if (existingDeviceId != null)
            {
                if (UpdateDeviceEvent(store, existingDeviceId, evt, calendar))
                    _mappingStore.SetMapping(evt.EventId, existingDeviceId, hash, deviceHash);
                else
                {
                    // Recreate if update failed
                    var deviceId = CreateDeviceEvent(store, evt, calendar);
                    if (deviceId != null)
                        _mappingStore.SetMapping(evt.EventId, deviceId, hash, deviceHash);
                    else
                        return Task.FromResult(false);
                }
            }
            else
            {
                var deviceId = CreateDeviceEvent(store, evt, calendar);
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
            if (deviceId == null)
                return Task.FromResult(false);

            var store = new EKEventStore();
            var deleted = DeleteDeviceEvent(store, deviceId);
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
            var store = new EKEventStore();
            var syncedIds = _mappingStore.GetAllSyncedServerEventIds();
            var deleted = 0;

            foreach (var syncedId in syncedIds)
            {
                ct.ThrowIfCancellationRequested();
                var deviceId = _mappingStore.GetDeviceEventId(syncedId);
                if (deviceId != null && DeleteDeviceEvent(store, deviceId))
                    deleted++;
            }

            // Remove the Famick calendar itself
            RemoveFamickCalendar(store);

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
            var store = new EKEventStore();
            var calendarId = Preferences.Get(CalendarIdPrefKey, null as string);
            if (calendarId == null)
                return Task.FromResult(result);

            var calendar = store.GetCalendar(calendarId);
            if (calendar == null)
                return Task.FromResult(result);

            var startDate = (NSDate)startUtc.ToLocalTime();
            var endDate = (NSDate)endUtc.ToLocalTime();
            var predicate = store.PredicateForEvents(startDate, endDate, new[] { calendar });
            var ekEvents = store.EventsMatching(predicate);

            if (ekEvents == null)
                return Task.FromResult(result);

            foreach (var ekEvent in ekEvents)
            {
                var eventData = new DeviceCalendarEventData
                {
                    DeviceEventId = ekEvent.EventIdentifier,
                    Title = ekEvent.Title ?? "",
                    Description = ekEvent.Notes,
                    Location = ekEvent.Location,
                    StartTimeUtc = ((DateTime)ekEvent.StartDate).ToUniversalTime(),
                    EndTimeUtc = ((DateTime)ekEvent.EndDate).ToUniversalTime(),
                    IsAllDay = ekEvent.AllDay,
                    RecurrenceRule = ekEvent.HasRecurrenceRules && ekEvent.RecurrenceRules?.Length > 0
                        ? SerializeRecurrenceRule(ekEvent.RecurrenceRules[0])
                        : null
                };

                // Read attendees
                if (ekEvent.Attendees != null)
                {
                    foreach (var attendee in ekEvent.Attendees)
                    {
                        var email = attendee.Url?.ResourceSpecifier; // mailto: URL -> email
                        var isRequired = attendee.ParticipantRole == EKParticipantRole.Required ||
                                         attendee.ParticipantRole == EKParticipantRole.Chair;

                        eventData.Attendees.Add(new DeviceCalendarAttendee
                        {
                            Name = attendee.Name,
                            Email = email,
                            IsRequired = isRequired
                        });
                    }
                }

                result.Add(eventData);
            }
        }
        catch
        {
            // Return whatever we have
        }

        return Task.FromResult(result);
    }

    #region EventKit Helpers

    private EKCalendar? GetOrCreateFamickCalendar(EKEventStore store)
    {
        // Try to get existing by stored identifier
        var calendarId = Preferences.Get(CalendarIdPrefKey, null as string);
        if (calendarId != null)
        {
            var existing = store.GetCalendar(calendarId);
            if (existing != null)
                return existing;
        }

        // Search by title in case it exists but ID wasn't stored
        var calendars = store.GetCalendars(EKEntityType.Event);
        foreach (var cal in calendars)
        {
            if (cal.Title == FamickCalendarTitle)
            {
                Preferences.Set(CalendarIdPrefKey, cal.CalendarIdentifier);
                return cal;
            }
        }

        // Create new calendar
        var newCalendar = EKCalendar.Create(EKEntityType.Event, store);
        newCalendar.Title = FamickCalendarTitle;
        newCalendar.CGColor = new CoreGraphics.CGColor(0.318f, 0.529f, 0.318f, 1.0f); // #518751

        // Find a suitable source (prefer local, then iCloud)
        EKSource? localSource = null;
        EKSource? icloudSource = null;

        foreach (var source in store.Sources)
        {
            if (source.SourceType == EKSourceType.Local)
                localSource = source;
            else if (source.SourceType == EKSourceType.CalDav)
                icloudSource = source;
        }

        newCalendar.Source = localSource ?? icloudSource ?? store.DefaultCalendarForNewEvents?.Source;
        if (newCalendar.Source == null)
            return null;

        NSError? error;
        if (store.SaveCalendar(newCalendar, true, out error) && error == null)
        {
            Preferences.Set(CalendarIdPrefKey, newCalendar.CalendarIdentifier);
            return newCalendar;
        }

        return null;
    }

    private void RemoveFamickCalendar(EKEventStore store)
    {
        var calendarId = Preferences.Get(CalendarIdPrefKey, null as string);
        if (calendarId == null) return;

        var calendar = store.GetCalendar(calendarId);
        if (calendar != null)
        {
            store.RemoveCalendar(calendar, true, out _);
        }

        Preferences.Remove(CalendarIdPrefKey);
    }

    private string? CreateDeviceEvent(EKEventStore store, CalendarOccurrence evt, EKCalendar calendar)
    {
        var ekEvent = EKEvent.FromStore(store);
        MapEventFields(ekEvent, evt);
        ekEvent.Calendar = calendar;

        NSError? error;
        if (store.SaveEvent(ekEvent, EKSpan.ThisEvent, out error) && error == null)
            return ekEvent.EventIdentifier;

        return null;
    }

    private bool UpdateDeviceEvent(EKEventStore store, string deviceEventId, CalendarOccurrence evt, EKCalendar calendar)
    {
        var ekEvent = store.EventFromIdentifier(deviceEventId);
        if (ekEvent == null)
            return false;

        MapEventFields(ekEvent, evt);
        ekEvent.Calendar = calendar;

        NSError? error;
        return store.SaveEvent(ekEvent, EKSpan.ThisEvent, out error) && error == null;
    }

    private bool DeleteDeviceEvent(EKEventStore store, string deviceEventId)
    {
        var ekEvent = store.EventFromIdentifier(deviceEventId);
        if (ekEvent == null)
            return true; // Already gone

        NSError? error;
        return store.RemoveEvent(ekEvent, EKSpan.ThisEvent, true, out error) && error == null;
    }

    private static void MapEventFields(EKEvent ekEvent, CalendarOccurrence evt)
    {
        ekEvent.Title = evt.Title;
        ekEvent.Notes = evt.Description;
        ekEvent.Location = evt.Location;
        ekEvent.AllDay = evt.IsAllDay;

        if (evt.IsAllDay)
        {
            // All-day events: use local date, set to midnight
            var startLocal = evt.StartTimeUtc.ToLocalTime().Date;
            var endLocal = evt.EndTimeUtc.ToLocalTime().Date;
            ekEvent.StartDate = (NSDate)startLocal;
            ekEvent.EndDate = (NSDate)endLocal;
        }
        else
        {
            ekEvent.StartDate = (NSDate)evt.StartTimeUtc;
            ekEvent.EndDate = (NSDate)evt.EndTimeUtc;
        }
    }

    /// <summary>
    /// Serializes an EKRecurrenceRule to an RFC 5545 RRULE string.
    /// Handles common patterns: DAILY, WEEKLY, MONTHLY, YEARLY with INTERVAL.
    /// </summary>
    private static string? SerializeRecurrenceRule(EKRecurrenceRule rule)
    {
        var freq = rule.Frequency switch
        {
            EKRecurrenceFrequency.Daily => "DAILY",
            EKRecurrenceFrequency.Weekly => "WEEKLY",
            EKRecurrenceFrequency.Monthly => "MONTHLY",
            EKRecurrenceFrequency.Yearly => "YEARLY",
            _ => null
        };

        if (freq == null) return null;

        var rrule = $"FREQ={freq}";

        if (rule.Interval > 1)
            rrule += $";INTERVAL={rule.Interval}";

        if (rule.RecurrenceEnd?.EndDate != null)
        {
            var endDate = (DateTime)rule.RecurrenceEnd.EndDate;
            rrule += $";UNTIL={endDate.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}";
        }
        else if (rule.RecurrenceEnd?.OccurrenceCount > 0)
        {
            rrule += $";COUNT={rule.RecurrenceEnd.OccurrenceCount}";
        }

        return rrule;
    }

    /// <summary>
    /// Computes a device-side hash from a server occurrence (what the device would look like).
    /// </summary>
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
