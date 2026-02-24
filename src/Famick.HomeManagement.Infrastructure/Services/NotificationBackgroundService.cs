using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Background service that runs daily notification evaluation.
/// Iterates all tenants, runs evaluators, checks preferences, deduplicates, and dispatches.
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockService _lockService;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly NotificationSettings _settings;

    private const string LockKey = "notification-daily-run";

    public NotificationBackgroundService(
        IServiceScopeFactory scopeFactory,
        IDistributedLockService lockService,
        IOptions<NotificationSettings> settings,
        ILogger<NotificationBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background service started. Daily run time: {RunTime} UTC",
            _settings.DailyRunTimeUtc);

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = CalculateDelayUntilNextRun();
            _logger.LogInformation("Next notification run in {Delay}", delay);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunDailyNotificationsAsync(stoppingToken);
        }

        _logger.LogInformation("Notification background service stopped");
    }

    private TimeSpan CalculateDelayUntilNextRun()
    {
        var now = DateTime.UtcNow;

        if (!TimeSpan.TryParse(_settings.DailyRunTimeUtc, out var runTime))
        {
            runTime = TimeSpan.FromHours(7); // Default 07:00 UTC
        }

        var nextRun = now.Date.Add(runTime);
        if (nextRun <= now)
        {
            nextRun = nextRun.AddDays(1);
        }

        return nextRun - now;
    }

    private async Task RunDailyNotificationsAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting daily notification evaluation");

        // Acquire distributed lock to prevent duplicate runs in multi-instance deployments
        await using var lockHandle = await _lockService.TryAcquireLockAsync(
            LockKey, TimeSpan.FromHours(1), stoppingToken);

        if (lockHandle is null)
        {
            _logger.LogInformation("Another instance is already running daily notifications. Skipping.");
            return;
        }

        try
        {
            // Get all tenant IDs
            var tenantIds = await GetAllTenantIdsAsync(stoppingToken);
            _logger.LogInformation("Running notification evaluation for {TenantCount} tenant(s)", tenantIds.Count);

            foreach (var tenantId in tenantIds)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await ProcessTenantAsync(tenantId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing notifications for tenant {TenantId}", tenantId);
                }
            }

            // Cleanup old notifications
            await CleanupOldNotificationsAsync(stoppingToken);

            _logger.LogInformation("Daily notification evaluation completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during daily notification evaluation");
        }
    }

    private async Task<List<Guid>> GetAllTenantIdsAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        // Tenant entity is NOT tenant-filtered (no ITenantEntity), so this returns all tenants
        return await dbContext.Tenants
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task ProcessTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing notifications for tenant {TenantId}", tenantId);

        using var scope = _scopeFactory.CreateScope();

        // Set tenant context for this scope
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.SetTenantId(tenantId);

        var evaluators = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationEvaluator>>();
        var dispatchers = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationDispatcher>>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        foreach (var evaluator in evaluators)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                var items = await evaluator.EvaluateAsync(tenantId, cancellationToken);

                if (items.Count == 0)
                {
                    _logger.LogDebug("Evaluator {EvaluatorType} produced no items for tenant {TenantId}",
                        evaluator.Type, tenantId);
                    continue;
                }

                _logger.LogInformation("Evaluator {EvaluatorType} produced {Count} item(s) for tenant {TenantId}",
                    evaluator.Type, items.Count, tenantId);

                foreach (var item in items)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    // Deduplicate: skip if user was already notified today for this type
                    var alreadyNotified = await notificationService.WasNotifiedTodayAsync(
                        item.UserId, item.Type, cancellationToken);

                    if (alreadyNotified)
                    {
                        _logger.LogDebug("User {UserId} already notified for {Type} today. Skipping.",
                            item.UserId, item.Type);
                        continue;
                    }

                    // Get user entity for dispatchers
                    var user = await dbContext.Users
                        .FirstOrDefaultAsync(u => u.Id == item.UserId, cancellationToken);

                    if (user is null || !user.IsActive)
                    {
                        _logger.LogDebug("User {UserId} not found or inactive. Skipping.", item.UserId);
                        continue;
                    }

                    // Get or create preference for this notification type
                    var preferences = await notificationService.GetPreferencesAsync(
                        item.UserId, cancellationToken);
                    var preference = preferences.FirstOrDefault(p => p.NotificationType == item.Type);

                    // Build a NotificationPreference entity from the DTO for dispatchers
                    var prefEntity = new Domain.Entities.NotificationPreference
                    {
                        TenantId = tenantId,
                        UserId = item.UserId,
                        NotificationType = item.Type,
                        EmailEnabled = preference?.EmailEnabled ?? true,
                        PushEnabled = preference?.PushEnabled ?? true,
                        InAppEnabled = preference?.InAppEnabled ?? true
                    };

                    // Dispatch to all channels
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
                                "Dispatcher {Dispatcher} failed for user {UserId}, type {Type}",
                                dispatcher.GetType().Name, item.UserId, item.Type);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Evaluator {EvaluatorType} failed for tenant {TenantId}",
                    evaluator.Type, tenantId);
            }
        }
    }

    private async Task CleanupOldNotificationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notificationService.CleanupOldNotificationsAsync(_settings.RetentionDays, cancellationToken);
            _logger.LogInformation("Cleaned up notifications older than {Days} days", _settings.RetentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up old notifications");
        }
    }
}
