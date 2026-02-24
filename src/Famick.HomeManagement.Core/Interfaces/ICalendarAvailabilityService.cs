using Famick.HomeManagement.Core.DTOs.Calendar;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for calculating free/busy information and finding available time slots.
/// Only "Involved" participation blocks availability; "Aware" does not.
/// External calendar events always count as busy.
/// </summary>
public interface ICalendarAvailabilityService
{
    /// <summary>
    /// Gets free/busy information for one or more users within a date range.
    /// </summary>
    Task<List<FreeBusyDto>> GetFreeBusyAsync(
        List<Guid> userIds,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds available time slots when all specified users are free.
    /// </summary>
    Task<List<AvailableSlotDto>> FindAvailableSlotsAsync(
        FindSlotsRequest request,
        CancellationToken cancellationToken = default);
}
