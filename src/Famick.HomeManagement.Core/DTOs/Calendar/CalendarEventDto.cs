using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Full calendar event response DTO with member details and exceptions.
/// </summary>
public class CalendarEventDto
{
    public Guid Id { get; set; }
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
    public Guid CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public List<CalendarEventMemberDto> Members { get; set; } = new();
    public List<CalendarEventExceptionDto> Exceptions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
