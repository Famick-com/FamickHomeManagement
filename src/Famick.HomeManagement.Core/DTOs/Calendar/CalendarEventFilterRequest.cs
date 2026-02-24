namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Filter parameters for querying calendar events within a date range.
/// </summary>
public class CalendarEventFilterRequest
{
    /// <summary>
    /// Start of the date range (inclusive).
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End of the date range (exclusive).
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Optional filter to specific user IDs. If empty, returns events for all household members.
    /// </summary>
    public List<Guid> UserIds { get; set; } = new();

    /// <summary>
    /// Whether to include events from external calendar subscriptions.
    /// </summary>
    public bool IncludeExternalEvents { get; set; } = true;
}
