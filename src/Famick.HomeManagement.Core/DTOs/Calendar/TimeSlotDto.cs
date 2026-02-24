namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Represents a time slot with optional title (used for busy periods).
/// </summary>
public class TimeSlotDto
{
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public string? Title { get; set; }
}
