using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Evaluates upcoming calendar events and produces reminder notifications
/// for "Involved" members within the event's reminder window.
/// Runs on a 5-minute polling interval (separate from the daily notification service).
/// </summary>
public class CalendarEventEvaluator : INotificationEvaluator
{
    private readonly HomeManagementDbContext _db;
    private readonly ILogger<CalendarEventEvaluator> _logger;

    public MessageType Type => MessageType.CalendarReminder;

    public CalendarEventEvaluator(
        HomeManagementDbContext db,
        ILogger<CalendarEventEvaluator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationItem>> EvaluateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var notifications = new List<NotificationItem>();

        // Resolve tenant time zone for display
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        var timeZone = ResolveTimeZone(tenant?.TimeZoneId);

        // Get all events with reminders that have "Involved" members
        var events = await _db.CalendarEvents
            .Include(e => e.Members)
            .Include(e => e.Exceptions)
            .Where(e => e.TenantId == tenantId)
            .Where(e => e.ReminderMinutesBefore.HasValue && e.ReminderMinutesBefore.Value > 0)
            .Where(e => e.Members.Any(m => m.ParticipationType == ParticipationType.Involved))
            .Where(e =>
                // Non-recurring: event hasn't ended yet
                (string.IsNullOrEmpty(e.RecurrenceRule) && e.EndTimeUtc > now) ||
                // Recurring: series hasn't ended
                (!string.IsNullOrEmpty(e.RecurrenceRule) &&
                 (!e.RecurrenceEndDate.HasValue || e.RecurrenceEndDate.Value > now)))
            .ToListAsync(cancellationToken);

        // Get existing calendar reminder notifications to avoid re-notifying
        var existingNotifications = await _db.Notifications
            .Where(n => n.TenantId == tenantId && n.Type == MessageType.CalendarReminder)
            .Where(n => n.CreatedAt >= now.AddDays(-1)) // Only check recent ones
            .Select(n => new { n.UserId, n.DeepLinkUrl })
            .ToListAsync(cancellationToken);

        var existingDeepLinks = existingNotifications
            .Where(n => n.DeepLinkUrl != null)
            .Select(n => $"{n.UserId}:{n.DeepLinkUrl}")
            .ToHashSet();

        foreach (var evt in events)
        {
            var reminderMinutes = evt.ReminderMinutesBefore!.Value;
            var involvedMembers = evt.Members
                .Where(m => m.ParticipationType == ParticipationType.Involved)
                .ToList();

            if (string.IsNullOrEmpty(evt.RecurrenceRule))
            {
                // Non-recurring event
                var reminderTime = evt.StartTimeUtc.AddMinutes(-reminderMinutes);

                if (now >= reminderTime && now < evt.StartTimeUtc)
                {
                    var deepLink = $"/calendar/events/{evt.Id}";

                    foreach (var member in involvedMembers)
                    {
                        var dedupeKey = $"{member.UserId}:{deepLink}";
                        if (existingDeepLinks.Contains(dedupeKey)) continue;

                        notifications.Add(BuildReminderNotification(
                            member.UserId, evt.Title, evt.StartTimeUtc, deepLink, timeZone));
                    }
                }
            }
            else
            {
                // Recurring event - find the next occurrence within reminder window
                var exceptions = evt.Exceptions.ToDictionary(ex => ex.OriginalStartTimeUtc, ex => ex);

                var lookAheadEnd = now.AddMinutes(reminderMinutes + 5);

                var calendar = new Calendar();
                var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
                {
                    DtStart = new CalDateTime(evt.StartTimeUtc, "UTC"),
                    DtEnd = new CalDateTime(evt.EndTimeUtc, "UTC")
                };
                icalEvent.RecurrenceRules.Add(new RecurrencePattern(evt.RecurrenceRule));
                calendar.Events.Add(icalEvent);

                var occurrences = icalEvent.GetOccurrences(
                    new CalDateTime(now.AddMinutes(-reminderMinutes), "UTC"))
                    .TakeWhileBefore(new CalDateTime(lookAheadEnd, "UTC"));

                foreach (var occurrence in occurrences)
                {
                    var occStart = occurrence.Period.StartTime.AsUtc;

                    if (evt.RecurrenceEndDate.HasValue && occStart > evt.RecurrenceEndDate.Value)
                        break;

                    if (exceptions.TryGetValue(occStart, out var exception) && exception.IsDeleted)
                        continue;

                    var actualStart = occStart;
                    if (exception != null && exception.OverrideStartTimeUtc.HasValue)
                        actualStart = exception.OverrideStartTimeUtc.Value;

                    var reminderTime = actualStart.AddMinutes(-reminderMinutes);

                    if (now >= reminderTime && now < actualStart)
                    {
                        var title = exception?.OverrideTitle ?? evt.Title;
                        var deepLink = $"/calendar/events/{evt.Id}?date={occStart:yyyy-MM-ddTHH:mm:ssZ}";

                        foreach (var member in involvedMembers)
                        {
                            var dedupeKey = $"{member.UserId}:{deepLink}";
                            if (existingDeepLinks.Contains(dedupeKey)) continue;

                            notifications.Add(BuildReminderNotification(
                                member.UserId, title, actualStart, deepLink, timeZone));
                        }
                    }
                }
            }
        }

        _logger.LogInformation("Calendar reminder evaluator produced {Count} notification(s) for tenant {TenantId}",
            notifications.Count, tenantId);

        return notifications;
    }

    private static NotificationItem BuildReminderNotification(
        Guid userId, string title, DateTime startTimeUtc, string deepLink, TimeZoneInfo timeZone)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, timeZone);
        var timeStr = localStart.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);
        var dateStr = localStart.ToString("dddd, MMMM d", System.Globalization.CultureInfo.InvariantCulture);
        var zoneStr = FormatTimeZoneLabel(localStart, timeZone);

        return new NotificationItem(
            userId,
            MessageType.CalendarReminder,
            $"Upcoming: {title}",
            $"Starts at {timeStr} {zoneStr} on {dateStr}",
            deepLink,
            new CalendarReminderData
            {
                EventTitle = title,
                StartTime = timeStr,
                StartDate = dateStr,
                TimeZoneLabel = zoneStr,
                DeepLinkUrl = deepLink
            }
        );
    }

    /// <summary>
    /// Resolves a household's time zone, falling back to UTC when the stored id is unusable.
    /// </summary>
    /// <remarks>
    /// The id comes from household settings, so it can be absent, stale, or simply wrong.
    /// <see cref="TimeZoneInfo.FindSystemTimeZoneById"/> throws on all three, and an
    /// unhandled throw here loses every reminder for the household rather than just
    /// mis-stating a time — a much worse failure than showing UTC. The reminder now names
    /// the zone it used, so a fallback is visible in the message instead of silent.
    /// </remarks>
    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId ?? "America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Utc;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    /// <summary>
    /// A short label naming the zone a reminder time is stated in, e.g. "EDT".
    /// </summary>
    /// <remarks>
    /// A bare "starts at 6:00 PM" is only unambiguous for someone sitting in the household's
    /// zone. The names come from the platform's time zone data and are not guaranteed to be
    /// multi-word English, so anything that does not reduce to initials cleanly falls back to
    /// the UTC offset, which is unambiguous even if it reads less naturally.
    /// </remarks>
    private static string FormatTimeZoneLabel(DateTime localStart, TimeZoneInfo timeZone)
    {
        var name = timeZone.IsDaylightSavingTime(localStart)
            ? timeZone.DaylightName
            : timeZone.StandardName;

        var words = (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words.All(w => char.IsUpper(w[0])))
            return string.Concat(words.Select(w => w[0]));

        var offset = timeZone.GetUtcOffset(localStart);
        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return $"UTC{sign}{Math.Abs(offset.Hours):D2}:{Math.Abs(offset.Minutes):D2}";
    }
}
