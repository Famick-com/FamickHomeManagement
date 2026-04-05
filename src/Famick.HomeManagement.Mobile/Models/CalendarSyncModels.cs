namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Result of a calendar sync operation.
/// </summary>
public class CalendarSyncResult
{
    public bool Success { get; set; }
    public int Created { get; set; }
    public int Updated { get; set; }
    public int Deleted { get; set; }
    public int Failed { get; set; }
    public string? ErrorMessage { get; set; }

    public static CalendarSyncResult Ok(int created, int updated, int deleted) => new()
    {
        Success = true, Created = created, Updated = updated, Deleted = deleted
    };

    public static CalendarSyncResult Fail(string error) => new()
    {
        Success = false, ErrorMessage = error
    };
}

/// <summary>
/// Current sync status for the calendar sync settings UI.
/// </summary>
public class CalendarSyncStatus
{
    public int SyncedCount { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public bool HasPermission { get; set; }
}

/// <summary>
/// Represents an event read from the device's Famick calendar.
/// Used to detect local additions, edits, and deletions during the pull phase.
/// </summary>
public class DeviceCalendarEventData
{
    public string DeviceEventId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? RecurrenceRule { get; set; }

    /// <summary>
    /// Attendees read from the device calendar event.
    /// Used during pull phase to match against household members.
    /// </summary>
    public List<DeviceCalendarAttendee> Attendees { get; set; } = new();
}

/// <summary>
/// Represents an attendee on a device calendar event.
/// </summary>
public class DeviceCalendarAttendee
{
    public string? Name { get; set; }
    public string? Email { get; set; }

    /// <summary>
    /// Whether this attendee is required (true) or optional (false).
    /// Maps to: iOS EKParticipantRole.Required vs Optional;
    /// Android CalendarContract.Attendees TYPE_REQUIRED vs TYPE_OPTIONAL.
    /// </summary>
    public bool IsRequired { get; set; }
}
