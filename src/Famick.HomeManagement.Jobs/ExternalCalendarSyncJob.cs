using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Jobs;

public class ExternalCalendarSyncJob : IJob
{
    private const string LockKey = "external-calendar-sync";
    private static readonly TimeSpan LockExpiry = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockService _lockService;

    public ExternalCalendarSyncJob(
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
            logger.LogInformation("Another instance is already running external calendar sync. Skipping.");
            return;
        }

        var tenantIds = await GetAllTenantIdsAsync(ct);
        logger.LogInformation("Running external calendar sync for {TenantCount} tenant(s)", tenantIds.Count);

        foreach (var tenantId in tenantIds)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await SyncTenantSubscriptionsAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error syncing external calendars for tenant {TenantId}", tenantId);
            }
        }

        logger.LogInformation("External calendar sync completed");
    }

    private async Task<List<Guid>> GetAllTenantIdsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        return await dbContext.Tenants.Select(t => t.Id).ToListAsync(ct);
    }

    private async Task SyncTenantSubscriptionsAsync(Guid tenantId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.SetTenantId(tenantId);

        var externalCalendarService = scope.ServiceProvider.GetRequiredService<IExternalCalendarService>();
        await externalCalendarService.SyncDueSubscriptionsAsync(ct);
    }
}
