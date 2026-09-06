using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Picks up queued exports and runs them.
/// </summary>
/// <remarks>
/// <para>
/// A polling loop over the queue table rather than an in-memory queue, and that is the point: the
/// work survives the process. A request that queues an export can be served by one instance and
/// the export run by another, and a deploy midway through leaves a row that the next instance
/// notices instead of work that quietly vanished.
/// </para>
/// <para>
/// The poll interval is the latency a user waits before their export starts. Short enough not to
/// feel broken, long enough that an idle deployment is not running a query every second.
/// </para>
/// </remarks>
public sealed class DataTransferWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DataTransferWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Data transfer worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = PollInterval;

            try
            {
                var ranSomething = await RunNextAsync(stoppingToken);

                // Keep going while there is work, so a backlog drains rather than trickling.
                if (ranSomething) continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Back off rather than spin: if the database is unreachable, hammering it every
                // five seconds helps nobody.
                logger.LogError(ex, "Data transfer worker failed a cycle");
                delay = ErrorBackoff;
            }

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        logger.LogInformation("Data transfer worker stopped");
    }

    private async Task<bool> RunNextAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();

        // IgnoreQueryFilters: the worker has no tenant context yet — finding out whose work this
        // is, is the point of the query. The service sets the tenant before touching anything else.
        var next = await context.HouseholdDataTransfers
            .IgnoreQueryFilters()
            .Where(t => t.Status == HouseholdDataTransferStatus.Queued)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new { t.Id, t.Kind })
            .FirstOrDefaultAsync(ct);

        if (next == null) return false;

        if (next.Kind != HouseholdDataTransferKind.Export)
            return false;

        var service = scope.ServiceProvider.GetRequiredService<IHouseholdDataPortabilityService>();
        await service.RunExportAsync(next.Id, ct);

        return true;
    }
}
