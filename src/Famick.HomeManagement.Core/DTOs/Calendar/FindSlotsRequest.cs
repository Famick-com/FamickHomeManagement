namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to find available time slots when all specified users are free.
/// </summary>
public class FindSlotsRequest
{
    /// <summary>
    /// User IDs that must all be free during the found slots.
    /// </summary>
    public List<Guid> UserIds { get; set; } = new();

    /// <summary>
    /// Required duration of the slot in minutes.
    /// </summary>
    public int DurationMinutes { get; set; }

    /// <summary>
    /// Start of the search range.
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End of the search range (max 30 days from StartDate).
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Earliest hour of the day to consider (0-23), in the household's timezone. Null means no restriction.
    /// </summary>
    public int? PreferredStartHour { get; set; }

    /// <summary>
    /// Latest hour of the day to consider (0-23), in the household's timezone. Null means no restriction.
    /// </summary>
    public int? PreferredEndHour { get; set; }

    /// <summary>
    /// IANA timezone ID for interpreting preferred hours (e.g. "America/New_York").
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>
    /// Maximum number of slots to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;
}
