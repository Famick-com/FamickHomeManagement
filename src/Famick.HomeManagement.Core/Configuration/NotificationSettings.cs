namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Configuration settings for the notification system
/// </summary>
public class NotificationSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "NotificationSettings";

    /// <summary>
    /// Default number of days before BestBeforeDate to trigger an expiry warning.
    /// Can be overridden per-product via Product.ExpiryWarningDays.
    /// </summary>
    public int DefaultExpiryWarningDays { get; set; } = 3;

    /// <summary>
    /// Time of day (UTC) when the daily notification evaluation runs. Format: "HH:mm".
    /// </summary>
    public string DailyRunTimeUtc { get; set; } = "07:00";

    /// <summary>
    /// Number of days to retain notifications before automatic cleanup.
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}
