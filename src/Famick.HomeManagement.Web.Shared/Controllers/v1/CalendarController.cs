using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing calendar events with recurrence support
/// </summary>
[ApiController]
[Route("api/v1/calendar")]
[Authorize]
public class CalendarController : ApiControllerBase
{
    private readonly ICalendarEventService _calendarEventService;
    private readonly IExternalCalendarService _externalCalendarService;
    private readonly ICalendarAvailabilityService _availabilityService;
    private readonly IUserManagementService _userManagementService;

    public CalendarController(
        ICalendarEventService calendarEventService,
        IExternalCalendarService externalCalendarService,
        ICalendarAvailabilityService availabilityService,
        IUserManagementService userManagementService,
        ITenantProvider tenantProvider,
        ILogger<CalendarController> logger)
        : base(tenantProvider, logger)
    {
        _calendarEventService = calendarEventService;
        _externalCalendarService = externalCalendarService;
        _availabilityService = availabilityService;
        _userManagementService = userManagementService;
    }

    #region Calendar Events

    /// <summary>
    /// Gets expanded calendar occurrences within a date range
    /// </summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(List<CalendarOccurrenceDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetEvents(
        [FromQuery] CalendarEventFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting calendar events for tenant {TenantId} from {Start} to {End}",
            TenantId, filter.StartDate, filter.EndDate);

        var occurrences = await _calendarEventService.GetCalendarEventsAsync(filter, cancellationToken);

        return ApiResponse(occurrences);
    }

    /// <summary>
    /// Gets a single calendar event by ID with full details
    /// </summary>
    [HttpGet("events/{id}")]
    [ProducesResponseType(typeof(CalendarEventDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetEvent(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting calendar event {EventId} for tenant {TenantId}", id, TenantId);

        var calendarEvent = await _calendarEventService.GetCalendarEventAsync(id, cancellationToken);

        if (calendarEvent == null)
        {
            return NotFoundResponse("Calendar event not found");
        }

        return ApiResponse(calendarEvent);
    }

    /// <summary>
    /// Creates a new calendar event with members
    /// </summary>
    [HttpPost("events")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(CalendarEventDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateEvent(
        [FromBody] CreateCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        _logger.LogInformation("Creating calendar event '{Title}' for tenant {TenantId} by user {UserId}",
            request.Title, TenantId, userId.Value);

        var calendarEvent = await _calendarEventService.CreateCalendarEventAsync(
            request, userId.Value, cancellationToken);

        return CreatedAtAction(nameof(GetEvent), new { id = calendarEvent.Id }, calendarEvent);
    }

    /// <summary>
    /// Updates a calendar event with scope support for recurring events
    /// </summary>
    [HttpPut("events/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(CalendarEventDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateEvent(
        Guid id,
        [FromBody] UpdateCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating calendar event {EventId} for tenant {TenantId} with scope {EditScope}",
            id, TenantId, request.EditScope);

        var calendarEvent = await _calendarEventService.UpdateCalendarEventAsync(id, request, cancellationToken);

        return ApiResponse(calendarEvent);
    }

    /// <summary>
    /// Deletes a calendar event with scope support for recurring events
    /// </summary>
    [HttpDelete("events/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteEvent(
        Guid id,
        [FromQuery] RecurrenceEditScope? editScope = null,
        [FromQuery] DateTime? occurrenceStartTimeUtc = null,
        CancellationToken cancellationToken = default)
    {
        var request = new DeleteCalendarEventRequest
        {
            EditScope = editScope,
            OccurrenceStartTimeUtc = occurrenceStartTimeUtc
        };

        _logger.LogInformation("Deleting calendar event {EventId} for tenant {TenantId} with scope {EditScope}",
            id, TenantId, request.EditScope);

        await _calendarEventService.DeleteCalendarEventAsync(id, request, cancellationToken);

        return NoContent();
    }

    #endregion

    #region Dashboard

    /// <summary>
    /// Gets upcoming events for the dashboard widget
    /// </summary>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(List<CalendarOccurrenceDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetUpcomingEvents(
        [FromQuery] int days = 7,
        [FromQuery] Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting upcoming events for tenant {TenantId}, days={Days}, userId={UserId}",
            TenantId, days, userId);

        var occurrences = await _calendarEventService.GetUpcomingEventsAsync(days, userId, cancellationToken);

        return ApiResponse(occurrences);
    }

    #endregion

    #region Members

    /// <summary>
    /// Gets household members for the calendar member picker.
    /// Returns all users in the tenant with a flag indicating the current user.
    /// </summary>
    [HttpGet("members")]
    [ProducesResponseType(typeof(List<CalendarMemberItem>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetMembers(CancellationToken cancellationToken = default)
    {
        var currentUserId = GetCurrentUserId();
        var users = await _userManagementService.GetAllUsersAsync(cancellationToken);

        var members = users
            .Where(u => u.IsActive)
            .Select(u => new CalendarMemberItem
            {
                Id = u.Id,
                DisplayName = $"{u.FirstName} {u.LastName}".Trim(),
                IsCurrentUser = u.Id == currentUserId
            })
            .ToList();

        return ApiResponse(members);
    }

    #endregion

    #region Availability

    /// <summary>
    /// Gets free/busy information for one or more users within a date range
    /// </summary>
    [HttpGet("freebusy")]
    [ProducesResponseType(typeof(List<FreeBusyDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetFreeBusy(
        [FromQuery] List<Guid> userIds,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting free/busy for {UserCount} user(s) from {Start} to {End}",
            userIds.Count, startDate, endDate);

        var result = await _availabilityService.GetFreeBusyAsync(userIds, startDate, endDate, cancellationToken);

        return ApiResponse(result);
    }

    /// <summary>
    /// Finds available time slots when all specified users are free
    /// </summary>
    [HttpPost("find-slots")]
    [ProducesResponseType(typeof(List<AvailableSlotDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> FindAvailableSlots(
        [FromBody] FindSlotsRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Finding available slots for {UserCount} user(s), duration={Duration}min",
            request.UserIds.Count, request.DurationMinutes);

        var slots = await _availabilityService.FindAvailableSlotsAsync(request, cancellationToken);

        return ApiResponse(slots);
    }

    #endregion

    #region External Calendar Subscriptions

    /// <summary>
    /// Gets all external calendar subscriptions for the current user
    /// </summary>
    [HttpGet("subscriptions")]
    [ProducesResponseType(typeof(List<ExternalCalendarSubscriptionDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetSubscriptions(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        var subscriptions = await _externalCalendarService.GetSubscriptionsAsync(userId.Value, cancellationToken);

        return ApiResponse(subscriptions);
    }

    /// <summary>
    /// Gets a single external calendar subscription
    /// </summary>
    [HttpGet("subscriptions/{id}")]
    [ProducesResponseType(typeof(ExternalCalendarSubscriptionDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetSubscription(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _externalCalendarService.GetSubscriptionAsync(id, cancellationToken);

        if (subscription == null)
        {
            return NotFoundResponse("External calendar subscription not found");
        }

        return ApiResponse(subscription);
    }

    /// <summary>
    /// Creates a new external calendar subscription
    /// </summary>
    [HttpPost("subscriptions")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ExternalCalendarSubscriptionDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateSubscription(
        [FromBody] CreateExternalCalendarSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        _logger.LogInformation("Creating external calendar subscription '{Name}' for user {UserId}",
            request.Name, userId.Value);

        var subscription = await _externalCalendarService.CreateSubscriptionAsync(
            request, userId.Value, cancellationToken);

        return CreatedAtAction(nameof(GetSubscription), new { id = subscription.Id }, subscription);
    }

    /// <summary>
    /// Updates an external calendar subscription
    /// </summary>
    [HttpPut("subscriptions/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ExternalCalendarSubscriptionDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateSubscription(
        Guid id,
        [FromBody] UpdateExternalCalendarSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating external calendar subscription {SubscriptionId}", id);

        var subscription = await _externalCalendarService.UpdateSubscriptionAsync(id, request, cancellationToken);

        return ApiResponse(subscription);
    }

    /// <summary>
    /// Deletes an external calendar subscription and all imported events
    /// </summary>
    [HttpDelete("subscriptions/{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteSubscription(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting external calendar subscription {SubscriptionId}", id);

        await _externalCalendarService.DeleteSubscriptionAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Triggers an immediate sync of an external calendar subscription
    /// </summary>
    [HttpPost("subscriptions/{id}/sync")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SyncSubscription(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Triggering sync for external calendar subscription {SubscriptionId}", id);

        await _externalCalendarService.SyncSubscriptionAsync(id, cancellationToken);

        return NoContent();
    }

    #endregion

    private Guid? GetCurrentUserId()
    {
        return _tenantProvider.UserId;
    }
}

/// <summary>
/// Lightweight DTO for the calendar member picker
/// </summary>
public class CalendarMemberItem
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
}
