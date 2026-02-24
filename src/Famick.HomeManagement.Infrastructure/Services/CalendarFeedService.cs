using System.Security.Cryptography;
using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using ICalEvent = Ical.Net.CalendarComponents.CalendarEvent;
using Ical.Net.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class CalendarFeedService : ICalendarFeedService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<CalendarFeedService> _logger;

    // Feed includes events from 30 days ago to 90 days ahead
    private const int PastDays = 30;
    private const int FutureDays = 90;

    public CalendarFeedService(
        HomeManagementDbContext context,
        ILogger<CalendarFeedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<UserCalendarIcsTokenDto>> GetTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var tokens = await _context.UserCalendarIcsTokens
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return tokens.Select(MapToDto).ToList();
    }

    public async Task<UserCalendarIcsTokenDto> CreateTokenAsync(
        CreateIcsTokenRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var token = new UserCalendarIcsToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = GenerateSecureToken(),
            IsRevoked = false,
            Label = request.Label
        };

        _context.UserCalendarIcsTokens.Add(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created ICS token {TokenId} for user {UserId}", token.Id, userId);

        return MapToDto(token);
    }

    public async Task RevokeTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _context.UserCalendarIcsTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token == null)
            throw new EntityNotFoundException(nameof(UserCalendarIcsToken), tokenId);

        token.IsRevoked = true;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Revoked ICS token {TokenId}", tokenId);
    }

    public async Task DeleteTokenAsync(
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _context.UserCalendarIcsTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);

        if (token == null)
            throw new EntityNotFoundException(nameof(UserCalendarIcsToken), tokenId);

        _context.UserCalendarIcsTokens.Remove(token);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted ICS token {TokenId}", tokenId);
    }

    public async Task<string?> GenerateIcsFeedAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        // Look up token WITHOUT tenant query filter (unauthenticated request)
        var tokenEntity = await _context.UserCalendarIcsTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token && !t.IsRevoked, cancellationToken);

        if (tokenEntity == null)
        {
            _logger.LogWarning("ICS feed requested with invalid or revoked token");
            return null;
        }

        var userId = tokenEntity.UserId;
        var tenantId = tokenEntity.TenantId;
        var now = DateTime.UtcNow;
        var rangeStart = now.AddDays(-PastDays);
        var rangeEnd = now.AddDays(FutureDays);

        // Build the iCalendar
        var calendar = new Calendar();
        calendar.Properties.Add(new CalendarProperty("X-WR-CALNAME", "Famick Home Calendar"));
        calendar.Properties.Add(new CalendarProperty("X-WR-TIMEZONE", "UTC"));
        calendar.ProductId = "-//Famick//Home Management//EN";

        // Add calendar events where user is a member
        await AddCalendarEventsAsync(calendar, tenantId, userId, rangeStart, rangeEnd, cancellationToken);

        // Serialize to ICS string
        var serializer = new CalendarSerializer();
        return serializer.SerializeToString(calendar);
    }

    #region Private Methods

    private async Task AddCalendarEventsAsync(
        Calendar calendar,
        Guid tenantId,
        Guid userId,
        DateTime rangeStart,
        DateTime rangeEnd,
        CancellationToken cancellationToken)
    {
        var events = await _context.CalendarEvents
            .IgnoreQueryFilters()
            .Include(e => e.Members).ThenInclude(m => m.User)
            .Include(e => e.Exceptions)
            .Where(e => e.TenantId == tenantId)
            .Where(e => e.Members.Any(m => m.UserId == userId))
            .Where(e =>
                // Non-recurring: overlaps range
                (string.IsNullOrEmpty(e.RecurrenceRule) && e.StartTimeUtc < rangeEnd && e.EndTimeUtc > rangeStart) ||
                // Recurring: series starts before range end
                (!string.IsNullOrEmpty(e.RecurrenceRule) && e.StartTimeUtc < rangeEnd &&
                 (!e.RecurrenceEndDate.HasValue || e.RecurrenceEndDate.Value > rangeStart)))
            .ToListAsync(cancellationToken);

        foreach (var evt in events)
        {
            var icalEvent = new ICalEvent
            {
                Uid = evt.Id.ToString(),
                Summary = evt.Title,
                Description = evt.Description,
                Location = evt.Location,
                DtStart = new CalDateTime(evt.StartTimeUtc, "UTC"),
                DtEnd = new CalDateTime(evt.EndTimeUtc, "UTC"),
                IsAllDay = evt.IsAllDay,
                Created = new CalDateTime(evt.CreatedAt, "UTC"),
                LastModified = new CalDateTime(evt.UpdatedAt ?? evt.CreatedAt, "UTC")
            };

            // Add recurrence rule if present
            if (!string.IsNullOrEmpty(evt.RecurrenceRule))
            {
                icalEvent.RecurrenceRules.Add(new RecurrencePattern(evt.RecurrenceRule));

                // Add UNTIL if there's a recurrence end date
                if (evt.RecurrenceEndDate.HasValue)
                {
                    var rule = icalEvent.RecurrenceRules[0];
                    rule.Until = evt.RecurrenceEndDate.Value;
                }
            }

            // Add exceptions as EXDATE entries for deleted occurrences
            foreach (var exception in evt.Exceptions.Where(ex => ex.IsDeleted))
            {
                icalEvent.ExceptionDates.Add(new PeriodList
                {
                    new Period(new CalDateTime(exception.OriginalStartTimeUtc, "UTC"))
                });
            }

            // Add reminder as VALARM
            if (evt.ReminderMinutesBefore.HasValue)
            {
                var alarm = new Alarm
                {
                    Action = AlarmAction.Display,
                    Description = evt.Title,
                    Trigger = new Trigger(TimeSpan.FromMinutes(-evt.ReminderMinutesBefore.Value))
                };
                icalEvent.Alarms.Add(alarm);
            }

            // Add members as ATTENDEE entries with email addresses
            foreach (var member in evt.Members)
            {
                if (member.User == null || string.IsNullOrEmpty(member.User.Email))
                    continue;

                var attendee = new Attendee
                {
                    CommonName = $"{member.User.FirstName} {member.User.LastName}".Trim(),
                    Value = new Uri($"mailto:{member.User.Email}"),
                    Rsvp = member.ParticipationType == ParticipationType.Involved
                };
                icalEvent.Attendees.Add(attendee);
            }

            calendar.Events.Add(icalEvent);
        }

        _logger.LogDebug("Added {Count} calendar events to ICS feed for user {UserId}", events.Count, userId);
    }

    private static string GenerateSecureToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static UserCalendarIcsTokenDto MapToDto(UserCalendarIcsToken token)
    {
        return new UserCalendarIcsTokenDto
        {
            Id = token.Id,
            Token = token.Token,
            Label = token.Label,
            IsRevoked = token.IsRevoked,
            CreatedAt = token.CreatedAt
        };
    }

    #endregion
}
