using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Jobs;

public static class JobRunner
{
    public static async Task<int> RunAsync(IServiceProvider services, string jobKey, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger($"Job.{jobKey}");

        var job = scope.ServiceProvider.GetKeyedService<IJob>(jobKey);
        if (job is null)
        {
            logger.LogError("Unknown job key '{JobKey}'", jobKey);
            return 64; // EX_USAGE
        }

        try
        {
            logger.LogInformation("Starting job {JobKey}", jobKey);
            await job.RunJob(logger, ct);
            logger.LogInformation("Job {JobKey} completed", jobKey);
            return 0;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            logger.LogWarning("Job {JobKey} was canceled", jobKey);
            return 130; // SIGINT-style exit
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobKey} failed", jobKey);
            return 1;
        }
    }
}
