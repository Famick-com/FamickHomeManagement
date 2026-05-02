using Famick.HomeManagement.Jobs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Tests.Unit.Jobs;

public class JobRunnerTests
{
    [Fact]
    public async Task RunAsync_ReturnsZero_WhenJobSucceeds()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddKeyedScoped<IJob, FakeJob>("ok", (_, _) => new FakeJob(success: true));
        await using var sp = services.BuildServiceProvider();

        var exit = await JobRunner.RunAsync(sp, "ok", CancellationToken.None);

        exit.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_ReturnsOne_WhenJobThrows()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddKeyedScoped<IJob, FakeJob>("boom", (_, _) => new FakeJob(success: false));
        await using var sp = services.BuildServiceProvider();

        var exit = await JobRunner.RunAsync(sp, "boom", CancellationToken.None);

        exit.Should().Be(1);
    }

    [Fact]
    public async Task RunAsync_ReturnsSixtyFour_WhenJobKeyUnknown()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        await using var sp = services.BuildServiceProvider();

        var exit = await JobRunner.RunAsync(sp, "missing", CancellationToken.None);

        exit.Should().Be(64);
    }

    [Fact]
    public async Task RunAsync_ReturnsOneThirty_WhenCancellationRequestedDuringJob()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddKeyedScoped<IJob, CancellingJob>("cancel");
        await using var sp = services.BuildServiceProvider();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var exit = await JobRunner.RunAsync(sp, "cancel", cts.Token);

        exit.Should().Be(130);
    }

    private sealed class FakeJob : IJob
    {
        private readonly bool _success;
        public FakeJob(bool success) { _success = success; }
        public Task RunJob(ILogger logger, CancellationToken ct)
            => _success ? Task.CompletedTask : throw new InvalidOperationException("boom");
    }

    private sealed class CancellingJob : IJob
    {
        public Task RunJob(ILogger logger, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
