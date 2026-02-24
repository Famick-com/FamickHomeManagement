namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to create a new calendar event.
/// </summary>
public class CreateCalendarEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAllDay { get; set; }

    /// <summary>
    /// RFC 5545 RRULE string for recurring events. Null for one-time events.
    /// </summary>
    public string? RecurrenceRule { get; set; }

    /// <summary>
    /// Optional end date for the recurrence series.
    /// </summary>
    public DateTime? RecurrenceEndDate { get; set; }

    /// <summary>
    /// Minutes before the event to send a reminder. Null for no reminder.
    /// </summary>
    public int? ReminderMinutesBefore { get; set; }

    /// <summary>
    /// Display color for the event.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Members to add to the event with their participation types.
    /// </summary>
    public List<CalendarEventMemberRequest> Members { get; set; } = new();
}
