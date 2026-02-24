namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Configuration settings for the calendar feature.
/// </summary>
public class CalendarSettings
{
    public const string SectionName = "Calendar";

    /// <summary>
    /// Default interval (in minutes) between external calendar sync checks.
    /// </summary>
    public int ExternalSyncIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Default reminder time (in minutes) before an event.
    /// </summary>
    public int DefaultReminderMinutes { get; set; } = 15;

    /// <summary>
    /// Maximum number of external calendar subscriptions per user.
    /// </summary>
    public int MaxExternalCalendarsPerUser { get; set; } = 10;

    /// <summary>
    /// Interval (in minutes) for checking calendar reminders.
    /// </summary>
    public int ReminderCheckIntervalMinutes { get; set; } = 5;
}
