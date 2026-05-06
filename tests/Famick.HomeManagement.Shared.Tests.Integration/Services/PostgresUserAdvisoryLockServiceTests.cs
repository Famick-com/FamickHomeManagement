using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// Phase 1 — verifies <see cref="PostgresUserAdvisoryLockService"/> serializes
/// concurrent acquires for the same user and runs different users in parallel.
/// The lock wraps password-change and refresh-token-rotation critical sections, so
/// the contract here directly affects whether jwt_min_iat bumps and refresh-token
/// reuse-detection compose correctly under concurrency.
/// </summary>
public class PostgresUserAdvisoryLockServiceTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public PostgresUserAdvisoryLockServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private PostgresUserAdvisoryLockService CreateService() =>
        new(_fixture.CreateDbContext(),
            NullLogger<PostgresUserAdvisoryLockService>.Instance);

    [Fact]
    public async Task AcquireAsync_succeeds_for_uncontended_user()
    {
        var service = CreateService();

        await using var lockHandle = await service.AcquireAsync(
            Guid.NewGuid(), TimeSpan.FromSeconds(2));

        lockHandle.Should().NotBeNull();
    }

    [Fact]
    public async Task Concurrent_acquires_for_same_user_serialize()
    {
        var userId = Guid.NewGuid();
        var serviceA = CreateService();
        var serviceB = CreateService();

        var bAcquired = false;
        var bAcquiredAfterARelease = false;

        await using var aLock = await serviceA.AcquireAsync(userId, TimeSpan.FromSeconds(2));

        // B's acquire blocks until A releases.
        var bTask = Task.Run(async () =>
        {
            await using var bLock = await serviceB.AcquireAsync(userId, TimeSpan.FromSeconds(10));
            bAcquired = true;
            // Hold briefly so we can observe the ordering before releasing.
            await Task.Delay(50);
        });

        // Give B a chance to start its acquire loop. While A still holds, B must
        // not have acquired yet.
        await Task.Delay(200);
        bAcquired.Should().BeFalse(
            "B must not have acquired the lock while A still holds it");

        // Release A and watch B succeed.
        await aLock.DisposeAsync();
        await bTask;
        bAcquired.Should().BeTrue(
            "B must acquire the lock once A releases");
    }

    [Fact]
    public async Task Acquires_for_different_users_run_in_parallel()
    {
        var serviceA = CreateService();
        var serviceB = CreateService();

        // Both should acquire without one waiting on the other — different users
        // produce different advisory-lock keys.
        await using var aLock = await serviceA.AcquireAsync(Guid.NewGuid(), TimeSpan.FromSeconds(2));
        await using var bLock = await serviceB.AcquireAsync(Guid.NewGuid(), TimeSpan.FromSeconds(2));

        aLock.Should().NotBeNull();
        bLock.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_throws_LockAcquisitionTimeoutException_on_contention_past_deadline()
    {
        var userId = Guid.NewGuid();
        var serviceA = CreateService();
        var serviceB = CreateService();

        await using var aLock = await serviceA.AcquireAsync(userId, TimeSpan.FromSeconds(2));

        var act = async () => await serviceB.AcquireAsync(userId, TimeSpan.FromMilliseconds(300));

        await act.Should().ThrowAsync<LockAcquisitionTimeoutException>(
            "B's deadline passes before A releases — must surface as the typed timeout exception");
    }

    [Fact]
    public async Task Reacquire_after_release_succeeds()
    {
        var userId = Guid.NewGuid();
        var service = CreateService();

        await using (var firstLock = await service.AcquireAsync(userId, TimeSpan.FromSeconds(2)))
        {
            firstLock.Should().NotBeNull();
        }

        // After dispose, the same user can acquire again.
        await using var secondLock = await service.AcquireAsync(userId, TimeSpan.FromSeconds(2));
        secondLock.Should().NotBeNull();
    }
}
