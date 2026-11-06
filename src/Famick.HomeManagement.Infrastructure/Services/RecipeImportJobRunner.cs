using System.Collections.Concurrent;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>Runs recipe import jobs in the background.</summary>
public interface IRecipeImportJobRunner
{
    /// <summary>Starts the job, as the household and user that asked for it, and returns at once.</summary>
    void Start(Guid jobId, Guid tenantId, Guid? userId);

    /// <summary>Stops the job straight away if it's running on this instance.</summary>
    void Cancel(Guid jobId);
}

/// <summary>
/// Runs each import on its own scope, after the request that started it has ended. A job cancelled on
/// another instance is noticed by checking the job's row every couple of seconds.
/// </summary>
public sealed class RecipeImportJobRunner(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<RecipeImportJobRunner> logger) : IRecipeImportJobRunner
{
    /// <summary>
    /// The most an import may take: the page's 30 seconds, the photo's, and the rest. Well inside the
    /// time after which a running job is reported as interrupted.
    /// </summary>
    public static readonly TimeSpan RunLimit = TimeSpan.FromSeconds(90);

    public static readonly TimeSpan CancelCheckInterval = TimeSpan.FromSeconds(2);

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _running = new();

    public void Start(Guid jobId, Guid tenantId, Guid? userId)
    {
        var stop = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        stop.CancelAfter(RunLimit);
        _running[jobId] = stop;

        // Not on the request's execution context: its HttpContext, and the household and user read
        // from it, end with the response.
        using (ExecutionContext.SuppressFlow())
            _ = Task.Run(() => RunAsync(jobId, tenantId, userId, stop));
    }

    public void Cancel(Guid jobId)
    {
        if (_running.TryGetValue(jobId, out var stop))
        {
            try { stop.Cancel(); }
            catch (ObjectDisposedException) { /* finished meanwhile */ }
        }
    }

    private async Task RunAsync(Guid jobId, Guid tenantId, Guid? userId, CancellationTokenSource stop)
    {
        var watch = WatchForCancelAsync(jobId, tenantId, stop);
        try
        {
            await using var scope = Scope(tenantId, userId);
            await scope.ServiceProvider.GetRequiredService<IRecipeImportService>().RunJobAsync(jobId, stop.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Recipe import job {JobId} stopped unexpectedly", jobId);
        }
        finally
        {
            _running.TryRemove(jobId, out _);
            stop.Cancel();
            await watch;
            stop.Dispose();
        }
    }

    /// <summary>Stops the job when its row stops saying it's running: cancelled from another instance.</summary>
    private async Task WatchForCancelAsync(Guid jobId, Guid tenantId, CancellationTokenSource stop)
    {
        try
        {
            while (true)
            {
                await Task.Delay(CancelCheckInterval, stop.Token);

                await using var scope = Scope(tenantId, null);
                var status = await scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>().RecipeImportJobs
                    .Where(j => j.Id == jobId)
                    .Select(j => (RecipeImportJobStatus?)j.Status)
                    .FirstOrDefaultAsync(stop.Token);

                if (status != RecipeImportJobStatus.Running)
                {
                    stop.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The job ended.
        }
        catch (Exception ex)
        {
            // A missed check only means a cancel from another instance waits for the time limit.
            logger.LogWarning(ex, "Stopped checking recipe import job {JobId} for a cancel", jobId);
        }
    }

    private AsyncServiceScope Scope(Guid tenantId, Guid? userId)
    {
        var scope = scopeFactory.CreateAsyncScope();
        var tenant = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
        tenant.SetTenantId(tenantId);
        if (userId is { } user)
            tenant.SetUserId(user);
        return scope;
    }
}
