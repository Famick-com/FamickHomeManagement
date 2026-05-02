using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Messaging.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Jobs;

public class NotificationsDailyJob : IJob
{
    private const string LockKey = "notification-daily-run";
    private static readonly TimeSpan LockExpiry = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockService _lockService;
    private readonly NotificationSettings _settings;

    public NotificationsDailyJob(
        IServiceScopeFactory scopeFactory,
        IDistributedLockService lockService,
        IOptions<NotificationSettings> settings)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
        _settings = settings.Value;
    }

    public async Task RunJob(ILogger logger, CancellationToken ct)
    {
        await using var lockHandle = await _lockService.TryAcquireLockAsync(LockKey, LockExpiry, ct);
        if (lockHandle is null)
        {
            logger.LogInformation("Another instance is already running daily notifications. Skipping.");
            return;
        }

        var tenantIds = await GetAllTenantIdsAsync(ct);
        logger.LogInformation("Running notification evaluation for {TenantCount} tenant(s)", tenantIds.Count);

        foreach (var tenantId in tenantIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await ProcessTenantAsync(tenantId, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing notifications for tenant {TenantId}", tenantId);
            }
        }

        await CleanupOldNotificationsAsync(logger, ct);
        logger.LogInformation("Daily notification evaluation completed");
    }

    private async Task<List<Guid>> GetAllTenantIdsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        return await dbContext.Tenants.Select(t => t.Id).ToListAsync(ct);
    }

    private async Task ProcessTenantAsync(Guid tenantId, ILogger logger, CancellationToken ct)
    {
        logger.LogDebug("Processing notifications for tenant {TenantId}", tenantId);

        using var scope = _scopeFactory.CreateScope();

        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.SetTenantId(tenantId);

        var evaluators = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationEvaluator>>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

        foreach (var evaluator in evaluators)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var items = await evaluator.EvaluateAsync(tenantId, ct);

                if (items.Count == 0)
                {
                    logger.LogDebug("Evaluator {EvaluatorType} produced no items for tenant {TenantId}",
                        evaluator.Type, tenantId);
                    continue;
                }

                logger.LogInformation("Evaluator {EvaluatorType} produced {Count} item(s) for tenant {TenantId}",
                    evaluator.Type, items.Count, tenantId);

                var isSaturday = DateTime.UtcNow.DayOfWeek == DayOfWeek.Saturday;

                foreach (var item in items)
                {
                    if (ct.IsCancellationRequested) break;

                    var alreadyNotified = await notificationService.WasNotifiedTodayAsync(
                        item.UserId, item.Type, ct);
                    if (alreadyNotified)
                    {
                        logger.LogDebug("User {UserId} already notified for {Type} today. Skipping.",
                            item.UserId, item.Type);
                        continue;
                    }

                    if (!isSaturday)
                    {
                        var contentHash = ContentHasher.ComputeHash(item.Data);
                        var lastHash = await notificationService.GetLastContentHashAsync(
                            item.UserId, item.Type, ct);

                        if (lastHash != null && lastHash == contentHash)
                        {
                            logger.LogDebug(
                                "Content unchanged for user {UserId}, type {Type}. Skipping until Saturday.",
                                item.UserId, item.Type);
                            continue;
                        }
                    }

                    try
                    {
                        await messageService.SendAsync(item.UserId, item.Type, item.Data, ct);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to send {Type} to user {UserId}",
                            item.Type, item.UserId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Evaluator {EvaluatorType} failed for tenant {TenantId}",
                    evaluator.Type, tenantId);
            }
        }
    }

    private async Task CleanupOldNotificationsAsync(ILogger logger, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
            await notificationService.CleanupOldNotificationsAsync(_settings.RetentionDays, ct);
            logger.LogInformation("Cleaned up notifications older than {Days} days", _settings.RetentionDays);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error cleaning up old notifications");
        }
    }
}
