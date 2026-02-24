namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// DTO for an external calendar subscription.
/// </summary>
public class ExternalCalendarSubscriptionDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IcsUrl { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public bool IsActive { get; set; }
    public int EventCount { get; set; }
}
