using Famick.HomeManagement.Core.DTOs.Calendar;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for managing calendar events with recurrence support.
/// Handles CRUD operations, recurrence expansion, and scope-aware edits/deletes.
/// </summary>
public interface ICalendarEventService
{
    /// <summary>
    /// Gets expanded calendar occurrences within a date range, including both
    /// Famick events (with recurrence expanded) and optionally external calendar events.
    /// </summary>
    Task<List<CalendarOccurrenceDto>> GetCalendarEventsAsync(
        CalendarEventFilterRequest filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single calendar event by ID with full details.
    /// </summary>
    Task<CalendarEventDto?> GetCalendarEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new calendar event with members.
    /// </summary>
    Task<CalendarEventDto> CreateCalendarEventAsync(
        CreateCalendarEventRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates a calendar event with scope support for recurring events.
    /// - EntireSeries: updates the event entity directly
    /// - ThisOccurrence: creates an exception with overrides
    /// - ThisAndFuture: caps original series and creates a new event
    /// </summary>
    Task<CalendarEventDto> UpdateCalendarEventAsync(
        Guid eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a calendar event with scope support for recurring events.
    /// - EntireSeries: hard deletes the event
    /// - ThisOccurrence: creates an exception with IsDeleted=true
    /// - ThisAndFuture: sets RecurrenceEndDate on the original event
    /// </summary>
    Task DeleteCalendarEventAsync(
        Guid eventId,
        DeleteCalendarEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets upcoming events for the dashboard widget.
    /// Returns the next N occurrences starting from now, optionally filtered by user.
    /// </summary>
    Task<List<CalendarOccurrenceDto>> GetUpcomingEventsAsync(
        int days = 7,
        Guid? userId = null,
        CancellationToken cancellationToken = default);
}
