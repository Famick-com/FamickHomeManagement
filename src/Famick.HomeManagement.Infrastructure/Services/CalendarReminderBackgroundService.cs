using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Background service that polls every 5 minutes for calendar event reminders.
/// Separate from the daily NotificationBackgroundService since reminders need
/// more frequent evaluation. Runs only the CalendarEventEvaluator.
/// </summary>
public class CalendarReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockService _lockService;
    private readonly ILogger<CalendarReminderBackgroundService> _logger;
    private readonly CalendarSettings _settings;

    private const string LockKey = "calendar-reminder-check";

    public CalendarReminderBackgroundService(
        IServiceScopeFactory scopeFactory,
        IDistributedLockService lockService,
        IOptions<CalendarSettings> settings,
        ILogger<CalendarReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Calendar reminder service started. Check interval: {Interval} minutes",
            _settings.ReminderCheckIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.ReminderCheckIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunReminderCheckAsync(stoppingToken);
        }

        _logger.LogInformation("Calendar reminder service stopped");
    }

    private async Task RunReminderCheckAsync(CancellationToken stoppingToken)
    {
        await using var lockHandle = await _lockService.TryAcquireLockAsync(
            LockKey, TimeSpan.FromMinutes(10), stoppingToken);

        if (lockHandle is null)
        {
            _logger.LogInformation("Another instance is already running calendar reminder check. Skipping.");
            return;
        }

        try
        {
            var tenantIds = await GetAllTenantIdsAsync(stoppingToken);
            _logger.LogDebug("Running calendar reminder check for {TenantCount} tenant(s)", tenantIds.Count);

            foreach (var tenantId in tenantIds)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await ProcessTenantRemindersAsync(tenantId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking calendar reminders for tenant {TenantId}", tenantId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during calendar reminder check");
        }
    }

    private async Task<List<Guid>> GetAllTenantIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        return await dbContext.Tenants
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessTenantRemindersAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.SetTenantId(tenantId);

        // Get only the CalendarEventEvaluator
        var evaluators = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationEvaluator>>();
        var calendarEvaluator = evaluators.FirstOrDefault(e => e.Type == NotificationType.CalendarReminder);

        if (calendarEvaluator == null)
        {
            _logger.LogWarning("CalendarEventEvaluator not found in DI container");
            return;
        }

        var items = await calendarEvaluator.EvaluateAsync(tenantId, cancellationToken);

        if (items.Count == 0) return;

        _logger.LogInformation("Calendar reminder evaluator produced {Count} reminder(s) for tenant {TenantId}",
            items.Count, tenantId);

        // Dispatch notifications
        var dispatchers = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationDispatcher>>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        foreach (var item in items)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var user = await dbContext.Users
                .FirstOrDefaultAsync(u => u.Id == item.UserId, cancellationToken);

            if (user is null || !user.IsActive) continue;

            var preferences = await notificationService.GetPreferencesAsync(
                item.UserId, cancellationToken);
            var preference = preferences.FirstOrDefault(p => p.NotificationType == item.Type);

            var prefEntity = new Domain.Entities.NotificationPreference
            {
                TenantId = tenantId,
                UserId = item.UserId,
                NotificationType = item.Type,
                EmailEnabled = preference?.EmailEnabled ?? true,
                PushEnabled = preference?.PushEnabled ?? true,
                InAppEnabled = preference?.InAppEnabled ?? true
            };

            foreach (var dispatcher in dispatchers)
            {
                try
                {
                    await dispatcher.DispatchAsync(
                        item, prefEntity, user, tenantId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Dispatcher {Dispatcher} failed for calendar reminder to user {UserId}",
                        dispatcher.GetType().Name, item.UserId);
                }
            }
        }
    }
}
