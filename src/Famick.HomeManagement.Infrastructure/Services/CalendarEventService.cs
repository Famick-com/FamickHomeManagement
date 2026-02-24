using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class CalendarEventService : ICalendarEventService
{
    private readonly HomeManagementDbContext _context;
    private readonly IMapper _mapper;
    private readonly IFileAccessTokenService _fileAccessTokenService;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<CalendarEventService> _logger;

    public CalendarEventService(
        HomeManagementDbContext context,
        IMapper mapper,
        IFileAccessTokenService fileAccessTokenService,
        IFileStorageService fileStorageService,
        ILogger<CalendarEventService> logger)
    {
        _context = context;
        _mapper = mapper;
        _fileAccessTokenService = fileAccessTokenService;
        _fileStorageService = fileStorageService;
        _logger = logger;
    }

    public async Task<List<CalendarOccurrenceDto>> GetCalendarEventsAsync(
        CalendarEventFilterRequest filter,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting calendar events from {Start} to {End}", filter.StartDate, filter.EndDate);

        var occurrences = new List<CalendarOccurrenceDto>();

        // 1. Get Famick events that could have occurrences in the range
        var famickEvents = await GetEventsInRangeAsync(filter, cancellationToken);

        foreach (var evt in famickEvents)
        {
            var expanded = ExpandEventOccurrences(evt, filter.StartDate, filter.EndDate);
            occurrences.AddRange(expanded);
        }

        // 2. Get external calendar events if requested
        if (filter.IncludeExternalEvents)
        {
            var externalEvents = await GetExternalEventsInRangeAsync(filter, cancellationToken);
            var externalOccurrences = _mapper.Map<List<CalendarOccurrenceDto>>(externalEvents);

            // Populate owner profile image URLs for external events
            foreach (var (occ, evt) in externalOccurrences.Zip(externalEvents))
            {
                var contact = evt.Subscription?.User?.Contact;
                if (contact != null && !string.IsNullOrEmpty(contact.ProfileImageFileName))
                {
                    var token = _fileAccessTokenService.GenerateToken("contact-profile-image", contact.Id, contact.TenantId);
                    occ.OwnerProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(contact.Id, token);
                }
            }

            occurrences.AddRange(externalOccurrences);
        }

        // Populate member profile image URLs for all occurrences
        PopulateMemberProfileImageUrls(occurrences, famickEvents);

        return occurrences.OrderBy(o => o.StartTimeUtc).ToList();
    }

    public async Task<CalendarEventDto?> GetCalendarEventAsync(
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting calendar event: {EventId}", eventId);

        var evt = await _context.CalendarEvents
            .Include(e => e.CreatedByUser)
            .Include(e => e.Members).ThenInclude(m => m.User).ThenInclude(u => u!.Contact)
            .Include(e => e.Exceptions)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt == null)
            return null;

        var dto = _mapper.Map<CalendarEventDto>(evt);

        // Populate member profile image URLs
        foreach (var memberDto in dto.Members)
        {
            var member = evt.Members.FirstOrDefault(m => m.UserId == memberDto.UserId);
            var contact = member?.User?.Contact;
            if (contact != null && !string.IsNullOrEmpty(contact.ProfileImageFileName))
            {
                var token = _fileAccessTokenService.GenerateToken("contact-profile-image", contact.Id, contact.TenantId);
                memberDto.ProfileImageUrl = _fileStorageService.GetContactProfileImageUrl(contact.Id, token);
            }
        }

        return dto;
    }

    public async Task<CalendarEventDto> CreateCalendarEventAsync(
        CreateCalendarEventRequest request,
        Guid createdByUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating calendar event: {Title}", request.Title);

        var evt = _mapper.Map<CalendarEvent>(request);
        evt.Id = Guid.NewGuid();
        evt.CreatedByUserId = createdByUserId;

        // Ensure the creator is included as Involved
        if (request.Members.All(m => m.UserId != createdByUserId))
        {
            request.Members.Insert(0, new CalendarEventMemberRequest
            {
                UserId = createdByUserId,
                ParticipationType = ParticipationType.Involved
            });
        }

        // Add members
        foreach (var memberReq in request.Members)
        {
            evt.Members.Add(new CalendarEventMember
            {
                Id = Guid.NewGuid(),
                CalendarEventId = evt.Id,
                UserId = memberReq.UserId,
                ParticipationType = memberReq.ParticipationType
            });
        }

        _context.CalendarEvents.Add(evt);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar event created: {Id}", evt.Id);

        return (await GetCalendarEventAsync(evt.Id, cancellationToken))!;
    }

    public async Task<CalendarEventDto> UpdateCalendarEventAsync(
        Guid eventId,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating calendar event: {EventId}, Scope: {Scope}", eventId, request.EditScope);

        var evt = await _context.CalendarEvents
            .Include(e => e.Members)
            .Include(e => e.Exceptions)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt == null)
            throw new EntityNotFoundException(nameof(CalendarEvent), eventId);

        var isRecurring = !string.IsNullOrEmpty(evt.RecurrenceRule);

        if (isRecurring && request.EditScope.HasValue)
        {
            switch (request.EditScope.Value)
            {
                case RecurrenceEditScope.ThisOccurrence:
                    await UpdateSingleOccurrence(evt, request, cancellationToken);
                    break;

                case RecurrenceEditScope.ThisAndFuture:
                    await UpdateThisAndFuture(evt, request, cancellationToken);
                    break;

                case RecurrenceEditScope.EntireSeries:
                    UpdateEntireSeries(evt, request);
                    break;
            }
        }
        else
        {
            // Non-recurring event: direct update
            UpdateEntireSeries(evt, request);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar event updated: {Id}", eventId);

        return (await GetCalendarEventAsync(eventId, cancellationToken))!;
    }

    public async Task DeleteCalendarEventAsync(
        Guid eventId,
        DeleteCalendarEventRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting calendar event: {EventId}, Scope: {Scope}", eventId, request.EditScope);

        var evt = await _context.CalendarEvents
            .Include(e => e.Members)
            .Include(e => e.Exceptions)
            .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);

        if (evt == null)
            throw new EntityNotFoundException(nameof(CalendarEvent), eventId);

        var isRecurring = !string.IsNullOrEmpty(evt.RecurrenceRule);

        if (isRecurring && request.EditScope.HasValue)
        {
            switch (request.EditScope.Value)
            {
                case RecurrenceEditScope.ThisOccurrence:
                    if (!request.OccurrenceStartTimeUtc.HasValue)
                        throw new ArgumentException("OccurrenceStartTimeUtc is required for ThisOccurrence delete");

                    evt.Exceptions.Add(new CalendarEventException
                    {
                        Id = Guid.NewGuid(),
                        CalendarEventId = eventId,
                        OriginalStartTimeUtc = request.OccurrenceStartTimeUtc.Value,
                        IsDeleted = true
                    });
                    break;

                case RecurrenceEditScope.ThisAndFuture:
                    if (!request.OccurrenceStartTimeUtc.HasValue)
                        throw new ArgumentException("OccurrenceStartTimeUtc is required for ThisAndFuture delete");

                    evt.RecurrenceEndDate = request.OccurrenceStartTimeUtc.Value.AddSeconds(-1);
                    break;

                case RecurrenceEditScope.EntireSeries:
                    _context.CalendarEvents.Remove(evt);
                    break;
            }
        }
        else
        {
            // Non-recurring or no scope: hard delete
            _context.CalendarEvents.Remove(evt);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Calendar event deleted: {Id}", eventId);
    }

    public async Task<List<CalendarOccurrenceDto>> GetUpcomingEventsAsync(
        int days = 7,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new CalendarEventFilterRequest
        {
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(days),
            IncludeExternalEvents = true
        };

        if (userId.HasValue)
        {
            filter.UserIds = new List<Guid> { userId.Value };
        }

        return await GetCalendarEventsAsync(filter, cancellationToken);
    }

    #region Private Methods - Query

    private async Task<List<CalendarEvent>> GetEventsInRangeAsync(
        CalendarEventFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var query = _context.CalendarEvents
            .Include(e => e.Members).ThenInclude(m => m.User).ThenInclude(u => u!.Contact)
            .Include(e => e.Exceptions)
            .AsQueryable();

        // Non-recurring events: start time falls within range OR end time falls within range
        // Recurring events: start time is before range end (occurrences could fall within)
        query = query.Where(e =>
            // Non-recurring: overlaps with the range
            (string.IsNullOrEmpty(e.RecurrenceRule) && e.StartTimeUtc < filter.EndDate && e.EndTimeUtc > filter.StartDate) ||
            // Recurring: series starts before range end and hasn't ended before range start
            (!string.IsNullOrEmpty(e.RecurrenceRule) && e.StartTimeUtc < filter.EndDate &&
             (!e.RecurrenceEndDate.HasValue || e.RecurrenceEndDate.Value > filter.StartDate)));

        // Filter by user IDs if specified
        if (filter.UserIds.Count > 0)
        {
            query = query.Where(e => e.Members.Any(m => filter.UserIds.Contains(m.UserId)));
        }

        return await query.ToListAsync(cancellationToken);
    }

    private async Task<List<ExternalCalendarEvent>> GetExternalEventsInRangeAsync(
        CalendarEventFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var query = _context.ExternalCalendarEvents
            .Include(e => e.Subscription).ThenInclude(s => s!.User).ThenInclude(u => u!.Contact)
            .Where(e => e.Subscription!.IsActive)
            .Where(e => e.StartTimeUtc < filter.EndDate && e.EndTimeUtc > filter.StartDate);

        // Filter by user IDs if specified (external subscriptions belong to specific users)
        if (filter.UserIds.Count > 0)
        {
            query = query.Where(e => filter.UserIds.Contains(e.Subscription!.UserId));
        }

        return await query.OrderBy(e => e.StartTimeUtc).ToListAsync(cancellationToken);
    }

    #endregion

    #region Private Methods - Recurrence Expansion

    private List<CalendarOccurrenceDto> ExpandEventOccurrences(
        CalendarEvent evt,
        DateTime rangeStart,
        DateTime rangeEnd)
    {
        var occurrences = new List<CalendarOccurrenceDto>();
        var exceptions = evt.Exceptions.ToDictionary(
            ex => ex.OriginalStartTimeUtc,
            ex => ex);

        if (string.IsNullOrEmpty(evt.RecurrenceRule))
        {
            // Non-recurring: single occurrence
            occurrences.Add(BuildOccurrenceDto(evt, evt.StartTimeUtc, evt.EndTimeUtc, null));
            return occurrences;
        }

        // Parse RRULE and expand occurrences using Ical.Net
        var calendar = new Calendar();
        var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
        {
            DtStart = new CalDateTime(evt.StartTimeUtc, "UTC"),
            DtEnd = new CalDateTime(evt.EndTimeUtc, "UTC")
        };

        // Add the recurrence rule
        icalEvent.RecurrenceRules.Add(new RecurrencePattern(evt.RecurrenceRule));

        calendar.Events.Add(icalEvent);

        // Get occurrences in the requested range
        var icalOccurrences = icalEvent.GetOccurrences(
            new CalDateTime(rangeStart, "UTC"),
            new CalDateTime(rangeEnd, "UTC"));

        var eventDuration = evt.EndTimeUtc - evt.StartTimeUtc;

        foreach (var occurrence in icalOccurrences)
        {
            var occurrenceStart = occurrence.Period.StartTime.AsUtc;

            // Check recurrence end date
            if (evt.RecurrenceEndDate.HasValue && occurrenceStart > evt.RecurrenceEndDate.Value)
                break;

            // Check for exceptions
            if (exceptions.TryGetValue(occurrenceStart, out var exception))
            {
                if (exception.IsDeleted)
                    continue; // Skip deleted occurrences

                // Apply overrides
                var overrideStart = exception.OverrideStartTimeUtc ?? occurrenceStart;
                var overrideEnd = exception.OverrideEndTimeUtc ?? overrideStart.Add(eventDuration);

                occurrences.Add(new CalendarOccurrenceDto
                {
                    EventId = evt.Id,
                    Title = exception.OverrideTitle ?? evt.Title,
                    Description = exception.OverrideDescription ?? evt.Description,
                    Location = exception.OverrideLocation ?? evt.Location,
                    StartTimeUtc = overrideStart,
                    EndTimeUtc = overrideEnd,
                    IsAllDay = exception.OverrideIsAllDay ?? evt.IsAllDay,
                    Color = evt.Color,
                    IsExternal = false,
                    OriginalStartTimeUtc = occurrenceStart,
                    Members = evt.Members.Select(m => _mapper.Map<CalendarEventMemberDto>(m)).ToList()
                });
            }
            else
            {
                var occurrenceEnd = occurrenceStart.Add(eventDuration);
                occurrences.Add(BuildOccurrenceDto(evt, occurrenceStart, occurrenceEnd, occurrenceStart));
            }
        }

        return occurrences;
    }

    private CalendarOccurrenceDto BuildOccurrenceDto(
        CalendarEvent evt,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        DateTime? originalStartTimeUtc)
    {
        return new CalendarOccurrenceDto
        {
            EventId = evt.Id,
            Title = evt.Title,
            Description = evt.Description,
            Location = evt.Location,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = endTimeUtc,
            IsAllDay = evt.IsAllDay,
            Color = evt.Color,
            IsExternal = false,
            OriginalStartTimeUtc = originalStartTimeUtc,
            Members = evt.Members.Select(m => _mapper.Map<CalendarEventMemberDto>(m)).ToList()
        };
    }

    #endregion

    #region Private Methods - Scope-Aware Updates

    private void UpdateEntireSeries(CalendarEvent evt, UpdateCalendarEventRequest request)
    {
        _mapper.Map(request, evt);

        // Sync members: remove old, add new
        SyncMembers(evt, request.Members);
    }

    private async Task UpdateSingleOccurrence(
        CalendarEvent evt,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.OccurrenceStartTimeUtc.HasValue)
            throw new ArgumentException("OccurrenceStartTimeUtc is required for ThisOccurrence edit");

        var originalStart = request.OccurrenceStartTimeUtc.Value;

        // Check if an exception already exists for this occurrence
        var existing = await _context.CalendarEventExceptions
            .FirstOrDefaultAsync(ex =>
                ex.CalendarEventId == evt.Id &&
                ex.OriginalStartTimeUtc == originalStart,
                cancellationToken);

        if (existing != null)
        {
            // Update existing exception
            existing.OverrideTitle = request.Title;
            existing.OverrideDescription = request.Description;
            existing.OverrideLocation = request.Location;
            existing.OverrideStartTimeUtc = request.StartTimeUtc;
            existing.OverrideEndTimeUtc = request.EndTimeUtc;
            existing.OverrideIsAllDay = request.IsAllDay;
            existing.IsDeleted = false;
        }
        else
        {
            // Create new exception
            evt.Exceptions.Add(new CalendarEventException
            {
                Id = Guid.NewGuid(),
                CalendarEventId = evt.Id,
                OriginalStartTimeUtc = originalStart,
                IsDeleted = false,
                OverrideTitle = request.Title,
                OverrideDescription = request.Description,
                OverrideLocation = request.Location,
                OverrideStartTimeUtc = request.StartTimeUtc,
                OverrideEndTimeUtc = request.EndTimeUtc,
                OverrideIsAllDay = request.IsAllDay
            });
        }
    }

    private async Task UpdateThisAndFuture(
        CalendarEvent evt,
        UpdateCalendarEventRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.OccurrenceStartTimeUtc.HasValue)
            throw new ArgumentException("OccurrenceStartTimeUtc is required for ThisAndFuture edit");

        // Cap the original series just before this occurrence
        evt.RecurrenceEndDate = request.OccurrenceStartTimeUtc.Value.AddSeconds(-1);

        // Remove exceptions that fall on or after the split point
        var futureExceptions = evt.Exceptions
            .Where(ex => ex.OriginalStartTimeUtc >= request.OccurrenceStartTimeUtc.Value)
            .ToList();
        foreach (var ex in futureExceptions)
        {
            _context.CalendarEventExceptions.Remove(ex);
        }

        // Create a new event for the future portion
        var newEvent = _mapper.Map<CalendarEvent>(request);
        newEvent.Id = Guid.NewGuid();
        newEvent.CreatedByUserId = evt.CreatedByUserId;
        newEvent.TenantId = evt.TenantId;

        // Add members to the new event
        foreach (var memberReq in request.Members)
        {
            newEvent.Members.Add(new CalendarEventMember
            {
                Id = Guid.NewGuid(),
                CalendarEventId = newEvent.Id,
                UserId = memberReq.UserId,
                ParticipationType = memberReq.ParticipationType
            });
        }

        _context.CalendarEvents.Add(newEvent);
    }

    private void SyncMembers(CalendarEvent evt, List<CalendarEventMemberRequest> requestedMembers)
    {
        var requestedUserIds = requestedMembers.Select(r => r.UserId).ToHashSet();
        var existingByUserId = evt.Members.ToDictionary(m => m.UserId);

        // Remove members no longer in the request
        var toRemove = evt.Members.Where(m => !requestedUserIds.Contains(m.UserId)).ToList();
        foreach (var member in toRemove)
        {
            evt.Members.Remove(member);
            _context.CalendarEventMembers.Remove(member);
        }

        // Update existing members or add new ones
        foreach (var memberReq in requestedMembers)
        {
            if (existingByUserId.TryGetValue(memberReq.UserId, out var existing))
            {
                existing.ParticipationType = memberReq.ParticipationType;
            }
            else
            {
                evt.Members.Add(new CalendarEventMember
                {
                    Id = Guid.NewGuid(),
                    CalendarEventId = evt.Id,
                    UserId = memberReq.UserId,
                    ParticipationType = memberReq.ParticipationType
                });
            }
        }
    }

    #endregion

    #region Private Methods - Profile Images

    private void PopulateMemberProfileImageUrls(
        List<CalendarOccurrenceDto> occurrences,
        List<CalendarEvent> famickEvents)
    {
        // Build a lookup of userId -> profile image URL to avoid duplicate token generation
        var profileImageUrls = new Dictionary<Guid, string>();

        foreach (var evt in famickEvents)
        {
            foreach (var member in evt.Members)
            {
                if (profileImageUrls.ContainsKey(member.UserId)) continue;

                var contact = member.User?.Contact;
                if (contact != null && !string.IsNullOrEmpty(contact.ProfileImageFileName))
                {
                    var token = _fileAccessTokenService.GenerateToken("contact-profile-image", contact.Id, contact.TenantId);
                    profileImageUrls[member.UserId] = _fileStorageService.GetContactProfileImageUrl(contact.Id, token);
                }
            }
        }

        foreach (var occ in occurrences)
        {
            foreach (var memberDto in occ.Members)
            {
                if (profileImageUrls.TryGetValue(memberDto.UserId, out var url))
                {
                    memberDto.ProfileImageUrl = url;
                }
            }
        }
    }

    #endregion
}
