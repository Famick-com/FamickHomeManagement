using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents a subscription to an external ICS calendar feed for a specific user.
/// The system periodically syncs events from the ICS URL into ExternalCalendarEvent records.
/// </summary>
public class ExternalCalendarSubscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// The user who owns this subscription.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Display name for the subscription (e.g., "Work Calendar", "Google Calendar").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL of the ICS feed to sync from.
    /// </summary>
    public string IcsUrl { get; set; } = string.Empty;

    /// <summary>
    /// Display color for imported events on the calendar.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// How often to sync this feed, in minutes.
    /// </summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// When this feed was last successfully synced.
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Status message from the last sync attempt (e.g., "Success", "HTTP 404", "Parse error").
    /// </summary>
    public string? LastSyncStatus { get; set; }

    /// <summary>
    /// Whether this subscription is active and should be synced.
    /// </summary>
    public bool IsActive { get; set; } = true;

    #region Navigation Properties

    public virtual User? User { get; set; }

    public virtual ICollection<ExternalCalendarEvent> Events { get; set; } = new List<ExternalCalendarEvent>();

    #endregion
}
