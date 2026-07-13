using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Ical.Net;
using Ical.Net.DataTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Builds the "upcoming reminders" feed consumed by the mobile app's offline notification engine
/// (self-hosted mode). Projects domain state forward in time, giving each reminder an explicit
/// <c>FireAtUtc</c> so the device can schedule a local OS notification that fires without network.
///
/// The per-category query predicates deliberately MIRROR the existing daily-run evaluators
/// (<see cref="CalendarEventEvaluator"/>, <see cref="ExpiryEvaluator"/>, <see cref="LowStockEvaluator"/>,
/// <see cref="TaskSummaryEvaluator"/>) so cloud push and self-hosted local reminders stay consistent.
/// They are copied (not refactored out of the evaluators) so changing this feed never risks altering
/// the live cloud digest behaviour.
/// </summary>
public class UpcomingReminderService : IUpcomingReminderService
{
    // Local hour of day at which non-time-specific reminders (expiry, low-stock, task digests,
    // due-date items) fire. Mirrors the spirit of the daily digest without a per-tenant setting.
    private const int DigestLocalHour = 8;

    private readonly HomeManagementDbContext _db;
    private readonly NotificationSettings _settings;
    private readonly ILogger<UpcomingReminderService> _logger;

    public UpcomingReminderService(
        HomeManagementDbContext db,
        IOptions<NotificationSettings> settings,
        ILogger<UpcomingReminderService> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UpcomingReminderDto>> GetUpcomingAsync(
        Guid tenantId,
        Guid userId,
        int days,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowEnd = now.AddDays(days);

        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        var timeZone = ResolveTimeZone(tenant?.TimeZoneId);

        // Per-type push preference for this user (default enabled when no row — matches MessageService).
        var disabledTypes = await _db.NotificationPreferences
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.UserId == userId && !p.PushEnabled)
            .Select(p => p.MessageType)
            .ToListAsync(cancellationToken);
        var disabled = disabledTypes.ToHashSet();

        var reminders = new List<UpcomingReminderDto>();

        if (!disabled.Contains(MessageType.CalendarReminder))
            reminders.AddRange(await BuildCalendarRemindersAsync(tenantId, userId, now, windowEnd, timeZone, cancellationToken));

        if (!disabled.Contains(MessageType.Expiry))
            reminders.AddRange(await BuildExpiryRemindersAsync(tenantId, now, windowEnd, timeZone, cancellationToken));

        if (!disabled.Contains(MessageType.LowStock))
            reminders.AddRange(BuildLowStockDigest(await CountLowStockAsync(tenantId, cancellationToken), now, timeZone));

        if (!disabled.Contains(MessageType.TaskSummary))
            reminders.AddRange(await BuildTaskRemindersAsync(tenantId, now, windowEnd, timeZone, cancellationToken));

        // Only future reminders, ordered by fire time. (Individual builders already window-filter,
        // but this is the single authoritative guard.)
        var result = reminders
            .Where(r => r.FireAtUtc > now && r.FireAtUtc <= windowEnd)
            .OrderBy(r => r.FireAtUtc)
            .ToList();

        _logger.LogDebug(
            "Upcoming reminder feed produced {Count} item(s) for user {UserId} over {Days} day(s)",
            result.Count, userId, days);

        return result;
    }

    // ---- Calendar reminders (precise-time) --------------------------------------------------

    private async Task<List<UpcomingReminderDto>> BuildCalendarRemindersAsync(
        Guid tenantId, Guid userId, DateTime now, DateTime windowEnd, TimeZoneInfo timeZone,
        CancellationToken ct)
    {
        var result = new List<UpcomingReminderDto>();

        // Same predicate as CalendarEventEvaluator, but scoped to events where THIS user is Involved.
        var events = await _db.CalendarEvents
            .Include(e => e.Members)
            .Include(e => e.Exceptions)
            .Where(e => e.TenantId == tenantId)
            .Where(e => e.ReminderMinutesBefore.HasValue && e.ReminderMinutesBefore.Value > 0)
            .Where(e => e.Members.Any(m => m.UserId == userId && m.ParticipationType == ParticipationType.Involved))
            .Where(e =>
                (string.IsNullOrEmpty(e.RecurrenceRule) && e.EndTimeUtc > now) ||
                (!string.IsNullOrEmpty(e.RecurrenceRule) &&
                 (!e.RecurrenceEndDate.HasValue || e.RecurrenceEndDate.Value > now)))
            .ToListAsync(ct);

        foreach (var evt in events)
        {
            var reminderMinutes = evt.ReminderMinutesBefore!.Value;

            if (string.IsNullOrEmpty(evt.RecurrenceRule))
            {
                var fireAt = evt.StartTimeUtc.AddMinutes(-reminderMinutes);
                if (fireAt > now && fireAt <= windowEnd && evt.StartTimeUtc > now)
                {
                    var deepLink = $"/calendar/events/{evt.Id}";
                    var key = $"cal:{evt.Id}:{evt.StartTimeUtc:o}";
                    result.Add(BuildCalendarReminder(key, evt.Title, evt.StartTimeUtc, fireAt, deepLink, timeZone));
                }
                continue;
            }

            // Recurring — expand occurrences across the whole prefetch window (same Ical.Net usage as
            // CalendarEventEvaluator, but looking ahead `windowEnd` rather than one reminder interval).
            var exceptions = evt.Exceptions.ToDictionary(ex => ex.OriginalStartTimeUtc, ex => ex);

            var calendar = new Calendar();
            var icalEvent = new Ical.Net.CalendarComponents.CalendarEvent
            {
                DtStart = new CalDateTime(evt.StartTimeUtc, "UTC"),
                DtEnd = new CalDateTime(evt.EndTimeUtc, "UTC")
            };
            icalEvent.RecurrenceRules.Add(new RecurrencePattern(evt.RecurrenceRule));
            calendar.Events.Add(icalEvent);

            var occurrences = icalEvent
                .GetOccurrences(new CalDateTime(now, "UTC"))
                .TakeWhileBefore(new CalDateTime(windowEnd, "UTC"));

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

                var fireAt = actualStart.AddMinutes(-reminderMinutes);
                if (fireAt <= now || fireAt > windowEnd || actualStart <= now)
                    continue;

                var title = exception?.OverrideTitle ?? evt.Title;
                var deepLink = $"/calendar/events/{evt.Id}?date={occStart:yyyy-MM-ddTHH:mm:ssZ}";
                var key = $"cal:{evt.Id}:{occStart:o}";
                result.Add(BuildCalendarReminder(key, title, actualStart, fireAt, deepLink, timeZone));
            }
        }

        return result;
    }

    private UpcomingReminderDto BuildCalendarReminder(
        string key, string title, DateTime startTimeUtc, DateTime fireAtUtc, string deepLink, TimeZoneInfo tz)
    {
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, tz);
        var body = $"Starts at {localStart:h:mm tt} on {localStart:yyyy-MM-dd}";
        var displayTitle = $"Upcoming: {title}";
        return new UpcomingReminderDto(
            key, MessageType.CalendarReminder, fireAtUtc, displayTitle, body, deepLink,
            ComputeHash(MessageType.CalendarReminder, fireAtUtc, displayTitle, body, deepLink));
    }

    // ---- Expiry (per stock entry) -----------------------------------------------------------

    private async Task<List<UpcomingReminderDto>> BuildExpiryRemindersAsync(
        Guid tenantId, DateTime now, DateTime windowEnd, TimeZoneInfo timeZone, CancellationToken ct)
    {
        var result = new List<UpcomingReminderDto>();
        var defaultWarningDays = _settings.DefaultExpiryWarningDays;

        // Same base query as ExpiryEvaluator.
        var entries = await _db.Stock
            .Include(s => s.Product)
            .Include(s => s.Location)
            .Where(s => s.TenantId == tenantId
                && s.Product != null
                && s.Product.IsActive
                && s.BestBeforeDate != null
                && s.Amount > 0)
            .ToListAsync(ct);

        foreach (var s in entries)
        {
            var bestBefore = s.BestBeforeDate!.Value.Date;
            var warningDays = s.Product!.ExpiryWarningDays ?? defaultWarningDays;
            var warnDate = bestBefore.AddDays(-warningDays);

            // Fire at DigestLocalHour local time on the warning date; if that instant is already
            // past (item already inside its window or expired), fire at the next local morning so
            // the user is still reminded.
            var fireAt = LocalDateAtHourUtc(warnDate, timeZone);
            if (fireAt <= now)
                fireAt = NextLocalHourUtc(now, timeZone);

            if (fireAt <= now || fireAt > windowEnd)
                continue;

            var isExpired = bestBefore < now.Date;
            var location = s.Location?.Name ?? "Unknown";
            var title = isExpired ? $"Expired: {s.Product.Name}" : $"Expiring soon: {s.Product.Name}";
            var body = $"Best before {bestBefore:yyyy-MM-dd} · {location}";
            var deepLink = "/stock";
            var key = $"exp:{s.Id}:{bestBefore:yyyy-MM-dd}";
            result.Add(new UpcomingReminderDto(
                key, MessageType.Expiry, fireAt, title, body, deepLink,
                ComputeHash(MessageType.Expiry, fireAt, title, body, deepLink)));
        }

        return result;
    }

    // ---- Low stock (single digest) ----------------------------------------------------------

    private async Task<int> CountLowStockAsync(Guid tenantId, CancellationToken ct)
    {
        // Same query as LowStockEvaluator, reduced to a count.
        var lowStock = await _db.Products
            .Where(p => p.TenantId == tenantId && p.IsActive && p.MinStockAmount > 0)
            .Select(p => new
            {
                CurrentStock = _db.Stock
                    .Where(s => s.ProductId == p.Id && s.Amount > 0)
                    .Sum(s => (decimal?)s.Amount) ?? 0m,
                p.MinStockAmount
            })
            .Where(p => p.CurrentStock < p.MinStockAmount)
            .CountAsync(ct);
        return lowStock;
    }

    private List<UpcomingReminderDto> BuildLowStockDigest(int lowStockCount, DateTime now, TimeZoneInfo timeZone)
    {
        if (lowStockCount == 0)
            return new List<UpcomingReminderDto>();

        var fireAt = NextLocalHourUtc(now, timeZone);
        var title = $"{lowStockCount} item(s) low on stock";
        var body = $"{lowStockCount} below minimum stock";
        var deepLink = "/stock";
        // Key is date-stamped so a new digest is scheduled per day; content hash catches count changes.
        var key = $"low:{TimeZoneInfo.ConvertTimeFromUtc(fireAt, timeZone):yyyy-MM-dd}";
        return new List<UpcomingReminderDto>
        {
            new(key, MessageType.LowStock, fireAt, title, body, deepLink,
                ComputeHash(MessageType.LowStock, fireAt, title, body, deepLink))
        };
    }

    // ---- Tasks & maintenance (digest + date-anchored due items) -----------------------------

    private async Task<List<UpcomingReminderDto>> BuildTaskRemindersAsync(
        Guid tenantId, DateTime now, DateTime windowEnd, TimeZoneInfo timeZone, CancellationToken ct)
    {
        var result = new List<UpcomingReminderDto>();
        var today = now.Date;

        // --- Current-state digest (mirrors TaskSummaryEvaluator counts) ---
        var incompleteTodos = await _db.TodoItems
            .Where(t => t.TenantId == tenantId && !t.IsCompleted)
            .CountAsync(ct);

        var chores = await _db.Chores
            .Include(c => c.LogEntries)
            .Where(c => c.TenantId == tenantId && c.PeriodType != "manually" && c.PeriodDays != null)
            .ToListAsync(ct);

        var overdueChoreCount = chores.Count(c => ChoreNextDue(c) is { } due && due <= today);

        var overdueMaintenanceCount = await _db.VehicleMaintenanceSchedules
            .Where(s => s.TenantId == tenantId && s.IsActive && s.NextDueDate != null && s.NextDueDate <= today)
            .CountAsync(ct);

        var totalTasks = incompleteTodos + overdueChoreCount + overdueMaintenanceCount;
        if (totalTasks > 0)
        {
            var parts = new List<string>();
            if (incompleteTodos > 0) parts.Add($"{incompleteTodos} todo(s)");
            if (overdueChoreCount > 0) parts.Add($"{overdueChoreCount} overdue chore(s)");
            if (overdueMaintenanceCount > 0) parts.Add($"{overdueMaintenanceCount} vehicle maintenance due");

            var fireAt = NextLocalHourUtc(now, timeZone);
            var title = $"You have {totalTasks} pending task(s)";
            var body = string.Join(", ", parts);
            var deepLink = "/todos";
            var key = $"task:{TimeZoneInfo.ConvertTimeFromUtc(fireAt, timeZone):yyyy-MM-dd}";
            result.Add(new UpcomingReminderDto(
                key, MessageType.TaskSummary, fireAt, title, body, deepLink,
                ComputeHash(MessageType.TaskSummary, fireAt, title, body, deepLink)));
        }

        // --- Date-anchored future chore due dates ---
        foreach (var c in chores)
        {
            var due = ChoreNextDue(c);
            if (due is not { } dueDate || dueDate <= today) continue; // future only; overdue counted above
            var fireAt = LocalDateAtHourUtc(dueDate, timeZone);
            if (fireAt <= now || fireAt > windowEnd) continue;

            var title = $"Chore due: {c.Name}";
            var body = $"Due {dueDate:yyyy-MM-dd}";
            var deepLink = "/chores";
            var key = $"chore:{c.Id}:{dueDate:yyyy-MM-dd}";
            result.Add(new UpcomingReminderDto(
                key, MessageType.TaskSummary, fireAt, title, body, deepLink,
                ComputeHash(MessageType.TaskSummary, fireAt, title, body, deepLink)));
        }

        // --- Date-anchored future vehicle maintenance ---
        var upcomingMaintenance = await _db.VehicleMaintenanceSchedules
            .Where(s => s.TenantId == tenantId && s.IsActive && s.NextDueDate != null && s.NextDueDate > today)
            .ToListAsync(ct);

        foreach (var m in upcomingMaintenance)
        {
            var dueDate = m.NextDueDate!.Value.Date;
            var fireAt = LocalDateAtHourUtc(dueDate, timeZone);
            if (fireAt <= now || fireAt > windowEnd) continue;

            var title = $"Vehicle maintenance due: {m.Name}";
            var body = $"Due {dueDate:yyyy-MM-dd}";
            var deepLink = "/vehicles";
            var key = $"veh:{m.Id}:{dueDate:yyyy-MM-dd}";
            result.Add(new UpcomingReminderDto(
                key, MessageType.TaskSummary, fireAt, title, body, deepLink,
                ComputeHash(MessageType.TaskSummary, fireAt, title, body, deepLink)));
        }

        return result;
    }

    /// <summary>
    /// Next due date for a periodic chore = last completion + PeriodDays. Null when the chore has
    /// no period; a chore that has never been executed is treated as due today (mirrors
    /// TaskSummaryEvaluator's "never executed = overdue").
    /// </summary>
    private static DateTime? ChoreNextDue(Chore chore)
    {
        if (chore.PeriodDays is not { } periodDays) return null;

        var lastLog = chore.LogEntries?
            .Where(l => !l.Undone && !l.Skipped && l.TrackedTime.HasValue)
            .OrderByDescending(l => l.TrackedTime)
            .FirstOrDefault();

        if (lastLog?.TrackedTime is null)
            return DateTime.UtcNow.Date; // never executed → due now

        return lastLog.TrackedTime.Value.Date.AddDays(periodDays);
    }

    // ---- Helpers ----------------------------------------------------------------------------

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

    /// <summary>UTC instant for <paramref name="localDate"/> at <see cref="DigestLocalHour"/> in <paramref name="tz"/>.</summary>
    private static DateTime LocalDateAtHourUtc(DateTime localDate, TimeZoneInfo tz)
    {
        var local = new DateTime(localDate.Year, localDate.Month, localDate.Day, DigestLocalHour, 0, 0, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    /// <summary>The next <see cref="DigestLocalHour"/> local time strictly after <paramref name="nowUtc"/>, as UTC.</summary>
    private static DateTime NextLocalHourUtc(DateTime nowUtc, TimeZoneInfo tz)
    {
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
        var candidate = LocalDateAtHourUtc(localNow.Date, tz);
        if (candidate <= nowUtc)
            candidate = LocalDateAtHourUtc(localNow.Date.AddDays(1), tz);
        return candidate;
    }

    private static string ComputeHash(MessageType type, DateTime fireAtUtc, string title, string body, string? deepLink)
    {
        var canonical = $"{(int)type}|{fireAtUtc:o}|{title}|{body}|{deepLink}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }
}
