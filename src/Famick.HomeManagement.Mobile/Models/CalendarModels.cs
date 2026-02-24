namespace Famick.HomeManagement.Mobile.Models;

/// <summary>
/// Represents a single expanded occurrence on the calendar.
/// Maps to server-side CalendarOccurrenceDto.
/// </summary>
public class CalendarOccurrence
{
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public bool IsAllDay { get; set; }
    public string? Color { get; set; }
    public bool IsExternal { get; set; }
    public DateTime? OriginalStartTimeUtc { get; set; }
    public List<CalendarEventMember> Members { get; set; } = new();
    public string? OwnerDisplayName { get; set; }
    public string? OwnerProfileImageUrl { get; set; }

    /// <summary>
    /// Local start time for display.
    /// </summary>
    public DateTime StartTimeLocal => StartTimeUtc.ToLocalTime();

    /// <summary>
    /// Local end time for display.
    /// </summary>
    public DateTime EndTimeLocal => EndTimeUtc.ToLocalTime();
}

/// <summary>
/// Full calendar event details. Maps to server-side CalendarEventDto.
/// </summary>
public class CalendarEventDetail
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
    public List<CalendarEventMember> Members { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Event member with participation type.
/// </summary>
public class CalendarEventMember
{
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public int ParticipationType { get; set; } // 1=Involved, 2=Aware
    public string? ProfileImageUrl { get; set; }
}

/// <summary>
/// Request to create a calendar event.
/// </summary>
public class CreateCalendarEventMobileRequest
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
    public List<CalendarMemberRequest> Members { get; set; } = new();
}

/// <summary>
/// Request to update a calendar event.
/// </summary>
public class UpdateCalendarEventMobileRequest
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
    public List<CalendarMemberRequest> Members { get; set; } = new();
    public int? EditScope { get; set; } // 1=ThisOccurrence, 2=ThisAndFuture, 3=EntireSeries
    public DateTime? OccurrenceStartTimeUtc { get; set; }
}

/// <summary>
/// Request to delete a calendar event.
/// </summary>
public class DeleteCalendarEventMobileRequest
{
    public int? EditScope { get; set; }
    public DateTime? OccurrenceStartTimeUtc { get; set; }
}

/// <summary>
/// Member request for event create/update.
/// </summary>
public class CalendarMemberRequest
{
    public Guid UserId { get; set; }
    public int ParticipationType { get; set; } = 1; // Default Involved
}

/// <summary>
/// Household member for member picker. Maps to CalendarMemberItem from the API.
/// </summary>
public class HouseholdMember
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
}
