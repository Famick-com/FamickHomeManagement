namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Lightweight calendar event DTO for list views and dashboard widgets.
/// </summary>
public class CalendarEventSummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? Color { get; set; }
    public bool IsRecurring { get; set; }
    public int MemberCount { get; set; }
}
