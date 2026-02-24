using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request DTO for adding a member to a calendar event.
/// </summary>
public class CalendarEventMemberRequest
{
    public Guid UserId { get; set; }
    public ParticipationType ParticipationType { get; set; } = ParticipationType.Involved;
}
