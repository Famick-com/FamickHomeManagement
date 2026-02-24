namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Free/busy information for a single user within a date range.
/// </summary>
public class FreeBusyDto
{
    public Guid UserId { get; set; }
    public string UserDisplayName { get; set; } = string.Empty;
    public List<TimeSlotDto> BusySlots { get; set; } = new();
}
