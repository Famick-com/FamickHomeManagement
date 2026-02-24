namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to update an external calendar subscription.
/// </summary>
public class UpdateExternalCalendarSubscriptionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public bool IsActive { get; set; } = true;
    public int SyncIntervalMinutes { get; set; } = 60;
}
