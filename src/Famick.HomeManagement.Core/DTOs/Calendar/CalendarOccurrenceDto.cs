namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Represents a single expanded occurrence on the calendar.
/// Used for both Famick events (with recurrence expanded) and external calendar events.
/// </summary>
public class CalendarOccurrenceDto
{
    /// <summary>
    /// The source event ID. For Famick events, this is the CalendarEvent.Id.
    /// For external events, this is the ExternalCalendarEvent.Id.
    /// </summary>
    public Guid EventId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? Color { get; set; }

    /// <summary>
    /// Whether this occurrence is from an external calendar subscription.
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// For Famick events: the original start time of the occurrence in the recurrence series.
    /// Used to identify the occurrence for edit/delete operations.
    /// Null for non-recurring events and external events.
    /// </summary>
    public DateTime? OriginalStartTimeUtc { get; set; }

    /// <summary>
    /// Members involved in this event (empty for external events).
    /// </summary>
    public List<CalendarEventMemberDto> Members { get; set; } = new();

    /// <summary>
    /// For external events: display name of the user who owns the subscription.
    /// </summary>
    public string? OwnerDisplayName { get; set; }

    /// <summary>
    /// For external events: profile image URL of the subscription owner.
    /// </summary>
    public string? OwnerProfileImageUrl { get; set; }
}
