namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a household calendar event with optional recurrence.
/// Recurrence rules are stored as RFC 5545 RRULE strings and expanded on-the-fly at query time.
/// </summary>
public class CalendarEvent : BaseTenantEntity
{
    /// <summary>
    /// Event title displayed on the calendar.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional detailed description of the event.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional location where the event takes place.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Start time in UTC. For all-day events, this is midnight UTC of the start date.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// End time in UTC. For all-day events, this is midnight UTC of the day after the end date.
    /// </summary>
    public DateTime EndTimeUtc { get; set; }

    /// <summary>
    /// Whether this is an all-day event (no specific time).
    /// </summary>
    public bool IsAllDay { get; set; }

    /// <summary>
    /// RFC 5545 RRULE string for recurring events (e.g., "FREQ=WEEKLY;BYDAY=MO,WE,FR").
    /// Null for non-recurring events.
    /// </summary>
    public string? RecurrenceRule { get; set; }

    /// <summary>
    /// Optional end date for the recurrence series. Null means the recurrence continues indefinitely.
    /// Used by ThisAndFuture edits to cap the original series.
    /// </summary>
    public DateTime? RecurrenceEndDate { get; set; }

    /// <summary>
    /// Minutes before the event to send a reminder notification. Null means no reminder.
    /// </summary>
    public int? ReminderMinutesBefore { get; set; }

    /// <summary>
    /// Display color for the event on the calendar (hex or named color).
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// The user who created this event.
    /// </summary>
    public Guid CreatedByUserId { get; set; }

    #region Navigation Properties

    /// <summary>
    /// The user who created this event.
    /// </summary>
    public virtual User? CreatedByUser { get; set; }

    /// <summary>
    /// Household members participating in this event.
    /// </summary>
    public virtual ICollection<CalendarEventMember> Members { get; set; } = new List<CalendarEventMember>();

    /// <summary>
    /// Exceptions to the recurrence pattern (single-occurrence overrides or deletions).
    /// </summary>
    public virtual ICollection<CalendarEventException> Exceptions { get; set; } = new List<CalendarEventException>();

    #endregion
}
