using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Jobs;

public class CalendarRemindersJob : IJob
{
    private const string LockKey = "calendar-reminder-check";
    private static readonly TimeSpan LockExpiry = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockService _lockService;

    public CalendarRemindersJob(
        IServiceScopeFactory scopeFactory,
        IDistributedLockService lockService)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
    }

    public async Task RunJob(ILogger logger, CancellationToken ct)
    {
        await using var lockHandle = await _lockService.TryAcquireLockAsync(LockKey, LockExpiry, ct);
        if (lockHandle is null)
        {
            logger.LogInformation("Another instance is already running calendar reminder check. Skipping.");
            return;
        }

        var tenantIds = await GetAllTenantIdsAsync(ct);
        logger.LogDebug("Running calendar reminder check for {TenantCount} tenant(s)", tenantIds.Count);

        foreach (var tenantId in tenantIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await ProcessTenantRemindersAsync(tenantId, logger, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error checking calendar reminders for tenant {TenantId}", tenantId);
            }
        }
    }

    private async Task<List<Guid>> GetAllTenantIdsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        return await dbContext.Tenants.Select(t => t.Id).ToListAsync(ct);
    }

    private async Task ProcessTenantRemindersAsync(Guid tenantId, ILogger logger, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.SetTenantId(tenantId);

        var evaluators = scope.ServiceProvider.GetRequiredService<IEnumerable<INotificationEvaluator>>();
        var calendarEvaluator = evaluators.FirstOrDefault(e => e.Type == MessageType.CalendarReminder);

        if (calendarEvaluator == null)
        {
            logger.LogWarning("CalendarEventEvaluator not found in DI container");
            return;
        }

        var items = await calendarEvaluator.EvaluateAsync(tenantId, ct);
        if (items.Count == 0) return;

        logger.LogInformation("Calendar reminder evaluator produced {Count} reminder(s) for tenant {TenantId}",
            items.Count, tenantId);

        var messageService = scope.ServiceProvider.GetRequiredService<IMessageService>();

        foreach (var item in items)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await messageService.SendAsync(item.UserId, item.Type, item.Data, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send calendar reminder to user {UserId}", item.UserId);
            }
        }
    }
}
