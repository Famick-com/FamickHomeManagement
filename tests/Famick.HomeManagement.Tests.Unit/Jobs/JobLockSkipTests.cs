using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Jobs;

/// <summary>
/// Each job must short-circuit when the distributed lock is already held —
/// no scopes created, no DB hits. Verifies the idempotency invariant.
/// </summary>
public class JobLockSkipTests
{
    [Fact]
    public async Task NotificationsDailyJob_SkipsWork_WhenLockNotAcquired()
    {
        var (lockMock, scopeFactoryMock) = BuildLockSkipMocks();
        var job = new NotificationsDailyJob(
            scopeFactoryMock.Object,
            lockMock.Object,
            Options.Create(new NotificationSettings()));

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task CalendarRemindersJob_SkipsWork_WhenLockNotAcquired()
    {
        var (lockMock, scopeFactoryMock) = BuildLockSkipMocks();
        var job = new CalendarRemindersJob(scopeFactoryMock.Object, lockMock.Object);

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    [Fact]
    public async Task ExternalCalendarSyncJob_SkipsWork_WhenLockNotAcquired()
    {
        var (lockMock, scopeFactoryMock) = BuildLockSkipMocks();
        var job = new ExternalCalendarSyncJob(scopeFactoryMock.Object, lockMock.Object);

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        scopeFactoryMock.Verify(x => x.CreateScope(), Times.Never);
    }

    private static (Mock<IDistributedLockService>, Mock<IServiceScopeFactory>) BuildLockSkipMocks()
    {
        var lockMock = new Mock<IDistributedLockService>();
        lockMock
            .Setup(x => x.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IAsyncDisposable?)null);

        // Strict — fails the test if CreateScope is unexpectedly invoked.
        var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        return (lockMock, scopeFactoryMock);
    }
}
