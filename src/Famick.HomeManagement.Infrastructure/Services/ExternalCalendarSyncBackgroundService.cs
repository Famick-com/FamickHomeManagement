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
/// Background service that periodically syncs external calendar subscriptions.
/// Iterates all tenants and syncs subscriptions that are due based on their sync interval.
/// </summary>
public class ExternalCalendarSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDistributedLockService _lockService;
    private readonly ILogger<ExternalCalendarSyncBackgroundService> _logger;
    private readonly CalendarSettings _settings;

    private const string LockKey = "external-calendar-sync";

    public ExternalCalendarSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IDistributedLockService lockService,
        IOptions<CalendarSettings> settings,
        ILogger<ExternalCalendarSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _lockService = lockService;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("External calendar sync service started. Check interval: {Interval} minutes",
            _settings.ExternalSyncIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.ExternalSyncIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunSyncAsync(stoppingToken);
        }

        _logger.LogInformation("External calendar sync service stopped");
    }

    private async Task RunSyncAsync(CancellationToken stoppingToken)
    {
        // Acquire distributed lock to prevent duplicate runs in multi-instance deployments
        await using var lockHandle = await _lockService.TryAcquireLockAsync(
            LockKey, TimeSpan.FromMinutes(30), stoppingToken);

        if (lockHandle is null)
        {
            _logger.LogInformation("Another instance is already running external calendar sync. Skipping.");
            return;
        }

        try
        {
            // Get all tenant IDs
            var tenantIds = await GetAllTenantIdsAsync(stoppingToken);
            _logger.LogInformation("Running external calendar sync for {TenantCount} tenant(s)", tenantIds.Count);

            foreach (var tenantId in tenantIds)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    await SyncTenantSubscriptionsAsync(tenantId, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error syncing external calendars for tenant {TenantId}", tenantId);
                }
            }

            _logger.LogInformation("External calendar sync completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error during external calendar sync");
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

    private async Task SyncTenantSubscriptionsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        // Set tenant context for this scope
        var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenantProvider.SetTenantId(tenantId);

        var externalCalendarService = scope.ServiceProvider.GetRequiredService<IExternalCalendarService>();
        await externalCalendarService.SyncDueSubscriptionsAsync(cancellationToken);
    }
}
