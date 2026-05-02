using Microsoft.Extensions.Logging;

public interface IJob
{
    /// <summary>
    /// Runs the specified job
    /// </summary>
    /// <param name="logger">Logger</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns></returns>
    Task RunJob(ILogger logger, CancellationToken ct);
}