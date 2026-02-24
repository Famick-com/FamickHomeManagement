namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Response DTO for a calendar event recurrence exception.
/// </summary>
public class CalendarEventExceptionDto
{
    public Guid Id { get; set; }
    public DateTime OriginalStartTimeUtc { get; set; }
    public bool IsDeleted { get; set; }
    public string? OverrideTitle { get; set; }
    public string? OverrideDescription { get; set; }
    public string? OverrideLocation { get; set; }
    public DateTime? OverrideStartTimeUtc { get; set; }
    public DateTime? OverrideEndTimeUtc { get; set; }
    public bool? OverrideIsAllDay { get; set; }
}
