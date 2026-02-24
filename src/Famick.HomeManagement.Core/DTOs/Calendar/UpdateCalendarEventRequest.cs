using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to update an existing calendar event, with scope for recurring events.
/// </summary>
public class UpdateCalendarEventRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? RecurrenceRule { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? ReminderMinutesBefore { get; set; }
    public string? Color { get; set; }
    public List<CalendarEventMemberRequest> Members { get; set; } = new();

    /// <summary>
    /// For recurring events: scope of the edit operation.
    /// Required when editing a recurring event.
    /// </summary>
    public RecurrenceEditScope? EditScope { get; set; }

    /// <summary>
    /// For ThisOccurrence or ThisAndFuture edits: the original start time of the occurrence being edited.
    /// Required when EditScope is ThisOccurrence or ThisAndFuture.
    /// </summary>
    public DateTime? OccurrenceStartTimeUtc { get; set; }
}
