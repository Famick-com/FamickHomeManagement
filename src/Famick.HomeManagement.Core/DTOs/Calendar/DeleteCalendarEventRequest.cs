using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Calendar;

/// <summary>
/// Request to delete a calendar event, with scope for recurring events.
/// </summary>
public class DeleteCalendarEventRequest
{
    /// <summary>
    /// For recurring events: scope of the delete operation.
    /// Required when deleting a recurring event.
    /// </summary>
    public RecurrenceEditScope? EditScope { get; set; }

    /// <summary>
    /// For ThisOccurrence or ThisAndFuture deletes: the original start time of the occurrence being deleted.
    /// Required when EditScope is ThisOccurrence or ThisAndFuture.
    /// </summary>
    public DateTime? OccurrenceStartTimeUtc { get; set; }
}
