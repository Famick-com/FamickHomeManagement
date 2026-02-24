namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an exception to a recurring calendar event for a specific occurrence.
/// Can either delete the occurrence or override its properties.
/// </summary>
public class CalendarEventException : BaseEntity
{
    /// <summary>
    /// The recurring calendar event this exception applies to.
    /// </summary>
    public Guid CalendarEventId { get; set; }

    /// <summary>
    /// The original start time of the occurrence being modified or deleted.
    /// Used to identify which occurrence in the recurrence series this exception targets.
    /// </summary>
    public DateTime OriginalStartTimeUtc { get; set; }

    /// <summary>
    /// If true, this occurrence is deleted from the series.
    /// </summary>
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Override title for this occurrence. Null means use the series title.
    /// </summary>
    public string? OverrideTitle { get; set; }

    /// <summary>
    /// Override description for this occurrence.
    /// </summary>
    public string? OverrideDescription { get; set; }

    /// <summary>
    /// Override location for this occurrence.
    /// </summary>
    public string? OverrideLocation { get; set; }

    /// <summary>
    /// Override start time for this occurrence (e.g., rescheduled to a different time).
    /// </summary>
    public DateTime? OverrideStartTimeUtc { get; set; }

    /// <summary>
    /// Override end time for this occurrence.
    /// </summary>
    public DateTime? OverrideEndTimeUtc { get; set; }

    /// <summary>
    /// Override all-day flag for this occurrence.
    /// </summary>
    public bool? OverrideIsAllDay { get; set; }

    #region Navigation Properties

    public virtual CalendarEvent? CalendarEvent { get; set; }

    #endregion
}
