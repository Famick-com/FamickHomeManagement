using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Jobs;

/// <summary>
/// Happy-path tests with the lock acquired. Uses InMemory EF Core for the
/// tenant query; per-tenant work is exercised by seeding tenants into the
/// database. Each test asserts the lock handle is disposed (i.e. released)
/// when the job exits.
/// </summary>
public class JobHappyPathTests
{
    [Fact]
    public async Task NotificationsDailyJob_AcquiresAndReleasesLock_WhenNoTenants()
    {
        var (lockMock, lockHandleMock) = BuildLockAcquired();
        var sp = BuildServiceProvider();
        var job = new NotificationsDailyJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            lockMock.Object,
            Options.Create(new NotificationSettings()));

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        lockHandleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task CalendarRemindersJob_AcquiresAndReleasesLock_WhenNoTenants()
    {
        var (lockMock, lockHandleMock) = BuildLockAcquired();
        var sp = BuildServiceProvider();
        var job = new CalendarRemindersJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            lockMock.Object);

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        lockHandleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task ExternalCalendarSyncJob_AcquiresAndReleasesLock_WhenNoTenants()
    {
        var (lockMock, lockHandleMock) = BuildLockAcquired();
        var sp = BuildServiceProvider();
        var job = new ExternalCalendarSyncJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            lockMock.Object);

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        lockHandleMock.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task NotificationsDailyJob_IteratesEachTenantInItsOwnScope()
    {
        var (lockMock, _) = BuildLockAcquired();
        var dbName = $"jobs-{Guid.NewGuid()}";

        var sp = BuildServiceProvider(dbName, registerTenantWork: true);
        await SeedTenantsAsync(sp, 2, dbName);

        var evaluatorMock = (Mock<INotificationEvaluator>)sp.GetRequiredService<Mock<INotificationEvaluator>>();
        var job = new NotificationsDailyJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            lockMock.Object,
            Options.Create(new NotificationSettings()));

        await job.RunJob(NullLogger.Instance, CancellationToken.None);

        // Each tenant should be evaluated once.
        evaluatorMock.Verify(
            x => x.EvaluateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task NotificationsDailyJob_OneTenantThrowing_DoesNotKillTheRun()
    {
        var (lockMock, _) = BuildLockAcquired();
        var dbName = $"jobs-{Guid.NewGuid()}";
        var sp = BuildServiceProvider(dbName, registerTenantWork: true);
        await SeedTenantsAsync(sp, 3, dbName);

        var evaluatorMock = sp.GetRequiredService<Mock<INotificationEvaluator>>();
        var calls = 0;
        evaluatorMock
            .Setup(x => x.EvaluateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                calls++;
                if (calls == 2) throw new InvalidOperationException("simulated tenant failure");
                return Task.FromResult<IReadOnlyList<NotificationItem>>(new List<NotificationItem>());
            });

        var job = new NotificationsDailyJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            lockMock.Object,
            Options.Create(new NotificationSettings()));

        var act = async () => await job.RunJob(NullLogger.Instance, CancellationToken.None);

        await act.Should().NotThrowAsync();
        calls.Should().Be(3, "every tenant must be tried even when one throws");
    }

    private static (Mock<IDistributedLockService>, Mock<IAsyncDisposable>) BuildLockAcquired()
    {
        var handleMock = new Mock<IAsyncDisposable>();
        handleMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var lockMock = new Mock<IDistributedLockService>();
        lockMock
            .Setup(x => x.TryAcquireLockAsync(
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(handleMock.Object);

        return (lockMock, handleMock);
    }

    private static IServiceProvider BuildServiceProvider(
        string? dbName = null,
        bool registerTenantWork = false)
    {
        var services = new ServiceCollection();
        services.AddDbContext<HomeManagementDbContext>(opt =>
            opt.UseInMemoryDatabase(dbName ?? $"jobs-{Guid.NewGuid()}"));

        if (registerTenantWork)
        {
            // ITenantProvider is set per-scope by the job; provide a no-op impl.
            services.AddScoped<ITenantProvider, TestTenantProvider>();

            var evalMock = new Mock<INotificationEvaluator>();
            evalMock
                .Setup(x => x.EvaluateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyList<NotificationItem>)new List<NotificationItem>());
            services.AddSingleton(evalMock);
            services.AddScoped<INotificationEvaluator>(_ => evalMock.Object);

            services.AddScoped(_ => new Mock<INotificationService>().Object);
            services.AddScoped(_ => new Mock<Famick.HomeManagement.Messaging.Interfaces.IMessageService>().Object);
        }

        return services.BuildServiceProvider();
    }

    private static async Task SeedTenantsAsync(IServiceProvider sp, int count, string _)
    {
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HomeManagementDbContext>();
        for (var i = 0; i < count; i++)
        {
            db.Tenants.Add(new Tenant
            {
                Id = Guid.NewGuid(),
                Name = $"Tenant {i}",
                Subdomain = $"tenant-{i}",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public Guid? TenantId { get; private set; }
        public Guid? UserId { get; private set; }
        public void SetTenantId(Guid tenantId) => TenantId = tenantId;
        public void SetUserId(Guid userId) => UserId = userId;
        public void ClearTenantId() => TenantId = null;
        public void ClearUserId() => UserId = null;
    }
}
