namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an event imported from an external ICS calendar feed.
/// These events are read-only within Famick and always count as busy for availability.
/// </summary>
public class ExternalCalendarEvent : BaseEntity
{
    /// <summary>
    /// The subscription this event was imported from.
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// The UID from the original ICS VEVENT. Used for deduplication during sync.
    /// </summary>
    public string ExternalUid { get; set; } = string.Empty;

    /// <summary>
    /// Event title from the ICS feed.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Event start time in UTC.
    /// </summary>
    public DateTime StartTimeUtc { get; set; }

    /// <summary>
    /// Event end time in UTC.
    /// </summary>
    public DateTime EndTimeUtc { get; set; }

    /// <summary>
    /// Whether this is an all-day event.
    /// </summary>
    public bool IsAllDay { get; set; }

    #region Navigation Properties

    public virtual ExternalCalendarSubscription? Subscription { get; set; }

    #endregion
}
