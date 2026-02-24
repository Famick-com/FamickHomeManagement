using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Ical.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ExternalCalendarService : IExternalCalendarService
{
    private readonly HomeManagementDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ExternalCalendarService> _logger;
    private readonly CalendarSettings _settings;

    public ExternalCalendarService(
        HomeManagementDbContext context,
        HttpClient httpClient,
        IOptions<CalendarSettings> settings,
        ILogger<ExternalCalendarService> logger)
    {
        _context = context;
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<ExternalCalendarSubscriptionDto>> GetSubscriptionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscriptions = await _context.ExternalCalendarSubscriptions
            .Where(s => s.UserId == userId)
            .Include(s => s.Events)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return subscriptions.Select(MapToDto).ToList();
    }

    public async Task<ExternalCalendarSubscriptionDto?> GetSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.ExternalCalendarSubscriptions
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

        return subscription == null ? null : MapToDto(subscription);
    }

    public async Task<ExternalCalendarSubscriptionDto> CreateSubscriptionAsync(
        CreateExternalCalendarSubscriptionRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Check max subscriptions per user
        var currentCount = await _context.ExternalCalendarSubscriptions
            .CountAsync(s => s.UserId == userId, cancellationToken);

        if (currentCount >= _settings.MaxExternalCalendarsPerUser)
        {
            throw new InvalidOperationException(
                $"Maximum of {_settings.MaxExternalCalendarsPerUser} external calendar subscriptions per user reached.");
        }

        var subscription = new ExternalCalendarSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            IcsUrl = NormalizeIcsUrl(request.IcsUrl),
            Color = NormalizeColor(request.Color),
            SyncIntervalMinutes = request.SyncIntervalMinutes,
            IsActive = true
        };

        _context.ExternalCalendarSubscriptions.Add(subscription);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created external calendar subscription {Id} for user {UserId}: {Name}",
            subscription.Id, userId, subscription.Name);

        return MapToDto(subscription);
    }

    public async Task<ExternalCalendarSubscriptionDto> UpdateSubscriptionAsync(
        Guid subscriptionId,
        UpdateExternalCalendarSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.ExternalCalendarSubscriptions
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

        if (subscription == null)
            throw new EntityNotFoundException(nameof(ExternalCalendarSubscription), subscriptionId);

        subscription.Name = request.Name;
        subscription.Color = NormalizeColor(request.Color);
        subscription.IsActive = request.IsActive;
        subscription.SyncIntervalMinutes = request.SyncIntervalMinutes;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated external calendar subscription {Id}", subscriptionId);

        return MapToDto(subscription);
    }

    public async Task DeleteSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.ExternalCalendarSubscriptions
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

        if (subscription == null)
            throw new EntityNotFoundException(nameof(ExternalCalendarSubscription), subscriptionId);

        // EF Cascade delete will handle the events
        _context.ExternalCalendarSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted external calendar subscription {Id} with {EventCount} events",
            subscriptionId, subscription.Events.Count);
    }

    public async Task SyncSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _context.ExternalCalendarSubscriptions
            .Include(s => s.Events)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken);

        if (subscription == null)
            throw new EntityNotFoundException(nameof(ExternalCalendarSubscription), subscriptionId);

        await SyncSingleSubscriptionAsync(subscription, cancellationToken);
    }

    public async Task SyncDueSubscriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        // Find active subscriptions that are due for sync
        var dueSubscriptions = await _context.ExternalCalendarSubscriptions
            .Include(s => s.Events)
            .Where(s => s.IsActive)
            .Where(s => !s.LastSyncedAt.HasValue ||
                        s.LastSyncedAt.Value.AddMinutes(s.SyncIntervalMinutes) <= now)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} external calendar subscription(s) due for sync", dueSubscriptions.Count);

        foreach (var subscription in dueSubscriptions)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await SyncSingleSubscriptionAsync(subscription, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync subscription {Id}: {Name}",
                    subscription.Id, subscription.Name);
            }
        }
    }

    #region Private Methods

    private async Task SyncSingleSubscriptionAsync(
        ExternalCalendarSubscription subscription,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Syncing external calendar: {Id} ({Name}) from {Url}",
            subscription.Id, subscription.Name, subscription.IcsUrl);

        try
        {
            // Fetch the ICS feed
            var icsContent = await FetchIcsFeedAsync(subscription.IcsUrl, cancellationToken);

            // Parse the ICS content
            var calendar = Calendar.Load(icsContent);

            if (calendar?.Events == null || calendar.Events.Count == 0)
            {
                _logger.LogWarning("No events found in ICS feed for subscription {Id}", subscription.Id);
                subscription.LastSyncedAt = DateTime.UtcNow;
                subscription.LastSyncStatus = "Success (0 events)";
                await _context.SaveChangesAsync(cancellationToken);
                return;
            }

            // Build a lookup of existing events by ExternalUid
            var existingEvents = subscription.Events.ToDictionary(e => e.ExternalUid, e => e);
            var processedUids = new HashSet<string>();

            foreach (var icalEvent in calendar.Events)
            {
                var uid = icalEvent.Uid;
                if (string.IsNullOrEmpty(uid)) continue;

                processedUids.Add(uid);

                var startTimeUtc = icalEvent.DtStart?.AsUtc ?? DateTime.MinValue;
                var endTimeUtc = icalEvent.DtEnd?.AsUtc ?? startTimeUtc;
                var isAllDay = icalEvent.IsAllDay;
                var title = icalEvent.Summary ?? "(No Title)";

                if (existingEvents.TryGetValue(uid, out var existing))
                {
                    // Update existing event
                    existing.Title = title;
                    existing.StartTimeUtc = startTimeUtc;
                    existing.EndTimeUtc = endTimeUtc;
                    existing.IsAllDay = isAllDay;
                }
                else
                {
                    // Create new event
                    var newEvent = new ExternalCalendarEvent
                    {
                        Id = Guid.NewGuid(),
                        SubscriptionId = subscription.Id,
                        ExternalUid = uid,
                        Title = title,
                        StartTimeUtc = startTimeUtc,
                        EndTimeUtc = endTimeUtc,
                        IsAllDay = isAllDay
                    };
                    _context.ExternalCalendarEvents.Add(newEvent);
                }
            }

            // Remove events that are no longer in the feed
            var toRemove = subscription.Events
                .Where(e => !processedUids.Contains(e.ExternalUid))
                .ToList();
            foreach (var evt in toRemove)
            {
                _context.ExternalCalendarEvents.Remove(evt);
            }

            subscription.LastSyncedAt = DateTime.UtcNow;
            subscription.LastSyncStatus = $"Success ({calendar.Events.Count} events)";

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Synced subscription {Id}: {EventCount} events processed, {RemovedCount} removed",
                subscription.Id, calendar.Events.Count, toRemove.Count);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error syncing subscription {Id}: {Message}", subscription.Id, ex.Message);
            subscription.LastSyncStatus = $"HTTP Error: {ex.StatusCode}";
            subscription.LastSyncedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing subscription {Id}: {Message}", subscription.Id, ex.Message);
            subscription.LastSyncStatus = $"Error: {ex.Message}";
            subscription.LastSyncedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> FetchIcsFeedAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static readonly Dictionary<string, string> CssColorNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["red"] = "#F44336", ["blue"] = "#2196F3", ["green"] = "#4CAF50",
        ["yellow"] = "#FFEB3B", ["orange"] = "#FF9800", ["purple"] = "#9C27B0",
        ["pink"] = "#E91E63", ["teal"] = "#009688", ["cyan"] = "#00BCD4",
        ["brown"] = "#795548", ["gray"] = "#9E9E9E", ["grey"] = "#9E9E9E",
        ["indigo"] = "#3F51B5", ["amber"] = "#FFC107", ["black"] = "#000000",
        ["white"] = "#FFFFFF", ["lime"] = "#CDDC39", ["navy"] = "#1A237E",
        ["maroon"] = "#880E4F", ["olive"] = "#827717", ["aqua"] = "#00BCD4",
        ["silver"] = "#BDBDBD", ["fuchsia"] = "#E91E63", ["coral"] = "#FF7043",
        ["salmon"] = "#FF8A65", ["gold"] = "#FFD600", ["tomato"] = "#FF5722",
        ["violet"] = "#7C4DFF", ["crimson"] = "#D32F2F", ["khaki"] = "#F0E68C",
    };

    /// <summary>
    /// Converts CSS color names to hex format. Returns the value as-is if already hex.
    /// </summary>
    private static string? NormalizeColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return null;

        color = color.Trim();

        // Already hex
        if (color[0] == '#')
            return color;

        // Known CSS color name
        if (CssColorNames.TryGetValue(color, out var hex))
            return hex;

        // Unknown name - prepend # in case it's hex without the prefix
        return $"#{color}";
    }

    private static string NormalizeIcsUrl(string url)
    {
        // Convert webcal:// to https://
        if (url.StartsWith("webcal://", StringComparison.OrdinalIgnoreCase))
        {
            return "https://" + url["webcal://".Length..];
        }
        return url;
    }

    private static ExternalCalendarSubscriptionDto MapToDto(ExternalCalendarSubscription subscription)
    {
        return new ExternalCalendarSubscriptionDto
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            Name = subscription.Name,
            IcsUrl = subscription.IcsUrl,
            Color = subscription.Color,
            SyncIntervalMinutes = subscription.SyncIntervalMinutes,
            LastSyncedAt = subscription.LastSyncedAt,
            LastSyncStatus = subscription.LastSyncStatus,
            IsActive = subscription.IsActive,
            EventCount = subscription.Events.Count
        };
    }

    #endregion
}
