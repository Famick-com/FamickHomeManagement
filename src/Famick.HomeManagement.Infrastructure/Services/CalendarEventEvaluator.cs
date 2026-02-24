using Famick.HomeManagement.Core.Interfaces;
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

    public NotificationType Type => NotificationType.CalendarReminder;

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
            .Where(n => n.TenantId == tenantId && n.Type == NotificationType.CalendarReminder)
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

                // Check if we're within the reminder window (between reminder time and event start)
                if (now >= reminderTime && now < evt.StartTimeUtc)
                {
                    var deepLink = $"/calendar/events/{evt.Id}";

                    foreach (var member in involvedMembers)
                    {
                        var dedupeKey = $"{member.UserId}:{deepLink}";
                        if (existingDeepLinks.Contains(dedupeKey)) continue;

                        notifications.Add(BuildReminderNotification(
                            member.UserId, evt.Title, evt.StartTimeUtc, deepLink));
                    }
                }
            }
            else
            {
                // Recurring event - find the next occurrence within reminder window
                var exceptions = evt.Exceptions.ToDictionary(ex => ex.OriginalStartTimeUtc, ex => ex);
                var eventDuration = evt.EndTimeUtc - evt.StartTimeUtc;

                // Look ahead by max reminder window (event could have up to 7 days reminder)
                var lookAheadEnd = now.AddMinutes(reminderMinutes + 5); // +5 min buffer for polling interval

                var calendar = new Calendar();
                var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
                {
                    DtStart = new CalDateTime(evt.StartTimeUtc, "UTC"),
                    DtEnd = new CalDateTime(evt.EndTimeUtc, "UTC")
                };
                icalEvent.RecurrenceRules.Add(new RecurrencePattern(evt.RecurrenceRule));
                calendar.Events.Add(icalEvent);

                var occurrences = icalEvent.GetOccurrences(
                    new CalDateTime(now.AddMinutes(-reminderMinutes), "UTC"),
                    new CalDateTime(lookAheadEnd, "UTC"));

                foreach (var occurrence in occurrences)
                {
                    var occStart = occurrence.Period.StartTime.AsUtc;

                    // Check recurrence end date
                    if (evt.RecurrenceEndDate.HasValue && occStart > evt.RecurrenceEndDate.Value)
                        break;

                    // Check if this occurrence is deleted
                    if (exceptions.TryGetValue(occStart, out var exception) && exception.IsDeleted)
                        continue;

                    // Apply override times if applicable
                    var actualStart = occStart;
                    if (exception != null && exception.OverrideStartTimeUtc.HasValue)
                        actualStart = exception.OverrideStartTimeUtc.Value;

                    var reminderTime = actualStart.AddMinutes(-reminderMinutes);

                    // Check if we're within the reminder window
                    if (now >= reminderTime && now < actualStart)
                    {
                        var title = exception?.OverrideTitle ?? evt.Title;
                        var deepLink = $"/calendar/events/{evt.Id}?date={occStart:yyyy-MM-ddTHH:mm:ssZ}";

                        foreach (var member in involvedMembers)
                        {
                            var dedupeKey = $"{member.UserId}:{deepLink}";
                            if (existingDeepLinks.Contains(dedupeKey)) continue;

                            notifications.Add(BuildReminderNotification(
                                member.UserId, title, actualStart, deepLink));
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
        Guid userId, string title, DateTime startTimeUtc, string deepLink)
    {
        var timeStr = startTimeUtc.ToString("HH:mm 'UTC'");
        var dateStr = startTimeUtc.ToString("yyyy-MM-dd");

        return new NotificationItem(
            userId,
            NotificationType.CalendarReminder,
            $"Upcoming: {title}",
            $"Starts at {timeStr} on {dateStr}",
            deepLink,
            $"Reminder: {title}",
            $"<h2>Calendar Reminder</h2><p>Your event <strong>{title}</strong> starts at {timeStr} on {dateStr}.</p>",
            $"Calendar Reminder\n\nYour event \"{title}\" starts at {timeStr} on {dateStr}."
        );
    }
}
