namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to create a new external calendar subscription.
/// </summary>
public class CreateExternalCalendarSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string IcsUrl { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SyncIntervalMinutes { get; set; } = 60;
}
