namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Represents a time slot when all requested users are available.
/// </summary>
public class AvailableSlotDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}
