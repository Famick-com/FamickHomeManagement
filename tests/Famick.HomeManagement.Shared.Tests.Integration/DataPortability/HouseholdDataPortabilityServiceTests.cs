using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.DataPortability;

/// <summary>
/// Covers the service around the writer: scoping, queueing, expiry and recovery.
/// </summary>
public class HouseholdDataPortabilityServiceTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task RefusesToRunWithoutATenantInContext()
    {
        // The failure this guards against is not an exception, it is silence: the global query
        // filter reads an absent tenant as "no filter", so a household-wide read would return
        // every household on the platform and write them into a file somebody downloads.
        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, tenantId: null);

        await FluentActions
            .Awaiting(() => service.GetCapabilitiesAsync())
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task StartingASecondExportReturnsTheOneAlreadyRunning()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, tenantId);

        var first = await service.StartExportAsync(new StartExportRequest(), userId);
        var second = await service.StartExportAsync(new StartExportRequest(), userId);

        // Two large archives of the same household, built at once, serve nobody.
        second.Id.Should().Be(first.Id);

        (await db.HouseholdDataTransfers.CountAsync(t => t.TenantId == tenantId))
            .Should().Be(1);
    }

    [Fact]
    public async Task OneHouseholdCannotReachAnothersExport()
    {
        var (mine, myUserId) = await SeedHouseholdAsync();
        var (theirs, _) = await SeedHouseholdAsync();

        await using var db = fixture.CreateDbContext();

        var mineService = BuildService(db, mine);
        var export = await mineService.StartExportAsync(new StartExportRequest(), myUserId);

        // Looked up by id alone, this would be a cross-household read of somebody's archive.
        var theirsService = BuildService(fixture.CreateDbContext(), theirs);
        (await theirsService.GetExportAsync(export.Id)).Should().BeNull();
        (await theirsService.OpenArchiveAsync(export.Id, null, null)).Status
            .Should().Be(ExportDownloadStatus.Unavailable);
        (await theirsService.GetDownloadLinkAsync(export.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AnExpiredArchiveIsNotDownloadableEvenWhileTheBytesRemain()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        await using var db = fixture.CreateDbContext();

        var transfer = new HouseholdDataTransfer
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Kind = HouseholdDataTransferKind.Export,
            Status = HouseholdDataTransferStatus.Completed,
            RequestedByUserId = userId,
            ArchiveFileName = "famick-export-test.zip",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
        };
        db.HouseholdDataTransfers.Add(transfer);
        await db.SaveChangesAsync();

        // Storage still has the object. Expiry is answered from the record, so the two cannot
        // disagree depending on which side deleted first.
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetExportArchiveInfoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileInfo(1024, DateTime.UtcNow));

        var service = BuildService(db, tenantId, storage.Object);

        (await service.OpenArchiveAsync(transfer.Id, null, null)).Status
            .Should().Be(ExportDownloadStatus.Unavailable);
        (await service.GetDownloadLinkAsync(transfer.Id)).Should().BeNull();
    }

    [Fact]
    public async Task ARunAbandonedByADeadWorkerStopsBlockingTheHousehold()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        await using var db = fixture.CreateDbContext();

        db.HouseholdDataTransfers.Add(new HouseholdDataTransfer
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Kind = HouseholdDataTransferKind.Export,
            Status = HouseholdDataTransferStatus.Running,
            RequestedByUserId = userId,
            StartedAt = DateTime.UtcNow.AddHours(-2),
            HeartbeatAt = DateTime.UtcNow.AddHours(-2),
        });
        await db.SaveChangesAsync();

        var service = BuildService(db, tenantId);
        var capabilities = await service.GetCapabilitiesAsync();

        // Without this the household is stuck behind a run nothing will ever finish, and the only
        // way out is a database edit.
        capabilities.ActiveExportId.Should().BeNull();

        // Read on a fresh context, which is what a later request is. Reusing the one that seeded
        // the row would hand back its own tracked copy and prove nothing about what was written.
        await using var reader = fixture.CreateDbContext();
        var stuck = await reader.HouseholdDataTransfers.SingleAsync(t => t.TenantId == tenantId);

        stuck.Status.Should().Be(HouseholdDataTransferStatus.Failed);
        stuck.ErrorCode.Should().Be("WORKER_LOST");
    }

    [Fact]
    public async Task RunsAgainstTheRetryingStrategyTheApplicationActuallyConfigures()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        // The shared fixture builds a plain context, so every other test here runs without the
        // execution strategy the application configures. That gap hid a real failure: the export
        // opened a transaction on EF's connection, and NpgsqlRetryingExecutionStrategy refuses
        // user-initiated transactions outright. It only surfaced when someone pressed the button.
        var options = new DbContextOptionsBuilder<Infrastructure.Data.HomeManagementDbContext>()
            .UseNpgsql(fixture.ConnectionString, npgsql => npgsql.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(1),
                errorCodesToAdd: null))
            .Options;

        await using var db = new Infrastructure.Data.HomeManagementDbContext(options);
        var service = BuildService(db, tenantId);

        var queued = await service.StartExportAsync(new StartExportRequest { IncludeFiles = false }, userId);
        await service.RunExportAsync(queued.Id);

        var finished = await db.HouseholdDataTransfers.SingleAsync(t => t.Id == queued.Id);

        finished.Status.Should().Be(HouseholdDataTransferStatus.Completed,
            "the export must run under the same configuration the application uses. Error was: {0}",
            finished.ErrorMessage);
    }

    [Fact]
    public async Task ProgressAndHeartbeatAreWrittenOutsideTheArchiveSnapshot()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, tenantId);

        var queued = await service.StartExportAsync(new StartExportRequest { IncludeFiles = false }, userId);
        await service.RunExportAsync(queued.Id);

        // This does not observe progress mid-run — the call above returns only once the export is
        // finished, and racing it would make the test flaky for no gain. What it does prove is the
        // property that mid-run visibility depends on: the archive snapshot is opened READ ONLY on
        // its own connection, so if progress writes were still enlisted in it the export would
        // fail outright rather than merely hide its progress. A completed run with progress
        // recorded is that guarantee.
        await using var reader = fixture.CreateDbContext();
        var seen = await reader.HouseholdDataTransfers.SingleAsync(t => t.Id == queued.Id);

        seen.Status.Should().Be(HouseholdDataTransferStatus.Completed,
            "a progress write enlisted in the read-only snapshot would have failed the export");
        seen.ProgressTotal.Should().BeGreaterThan(0);
        seen.HeartbeatAt.Should().NotBeNull("a heartbeat is what tells a later request the worker was alive");
    }

    [Fact]
    public async Task ConcurrentRequestsCannotQueueTwoExportsForOneHousehold()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        // Separate contexts, so neither sees the other's uncommitted insert — which is exactly
        // the window the in-process check cannot close. Before the partial unique index, both
        // sides passed the check and the loser sat Queued forever: the worker lock only
        // serializes processing, and the reconciler only looks at Running.
        var services = Enumerable.Range(0, 5)
            .Select(_ => BuildService(fixture.CreateDbContext(), tenantId))
            .ToList();

        var results = await Task.WhenAll(services.Select(svc =>
            svc.StartExportAsync(new StartExportRequest(), userId)));

        results.Select(r => r.Id).Distinct().Should().ContainSingle(
            "every caller should end up with the same export");

        await using var db = fixture.CreateDbContext();
        (await db.HouseholdDataTransfers.CountAsync(t => t.TenantId == tenantId))
            .Should().Be(1, "the database is what actually decides, not the read-then-insert check");
    }

    [Fact]
    public async Task AnExportStillRunningCannotBeDeletedFromUnderTheWorker()
    {
        var (tenantId, userId) = await SeedHouseholdAsync();

        await using var db = fixture.CreateDbContext();

        var running = new HouseholdDataTransfer
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Kind = HouseholdDataTransferKind.Export,
            Status = HouseholdDataTransferStatus.Running,
            RequestedByUserId = userId,
            HeartbeatAt = DateTime.UtcNow,
        };
        db.HouseholdDataTransfers.Add(running);
        await db.SaveChangesAsync();

        // Marking a running export Expired does not stop the worker. It would go on to save the
        // archive, write Completed over the Expired, and email a link to something the user
        // believes they deleted.
        (await BuildService(db, tenantId).DeleteExportAsync(running.Id)).Should().BeFalse();

        var after = await db.HouseholdDataTransfers.SingleAsync(t => t.Id == running.Id);
        after.Status.Should().Be(HouseholdDataTransferStatus.Running);
    }

    [Theory]
    // Explicit, open-ended, and suffix — the third is the one that reads as "no range" if the
    // start is taken at face value, because a suffix request arrives with no start at all.
    [InlineData(2L, 5L, 2L, 5L)]
    [InlineData(4L, null, 4L, 9L)]
    [InlineData(null, 3L, 7L, 9L)]
    public async Task RangesResolveAgainstTheArchiveLength(
        long? requestedStart, long? requestedEnd, long expectedStart, long expectedEnd)
    {
        var (transferId, service) = await CompletedExportOfLengthAsync(10);

        var result = await service.OpenArchiveAsync(transferId, requestedStart, requestedEnd);

        result.Status.Should().Be(ExportDownloadStatus.Ok);
        result.Download!.RangeStart.Should().Be(expectedStart);
        result.Download.RangeEnd.Should().Be(expectedEnd);
    }

    [Theory]
    [InlineData(10L, null)]   // starts exactly at the end
    [InlineData(100L, null)]  // starts well past it
    [InlineData(8L, 4L)]      // end before start
    public async Task RangesOutsideTheArchiveAreRefusedRatherThanServedEmpty(long? start, long? end)
    {
        var (transferId, service) = await CompletedExportOfLengthAsync(10);

        // Answering with zero bytes and a 206 would tell the client the archive ended where it
        // did not. Before this, the bound came out negative and the response length with it.
        (await service.OpenArchiveAsync(transferId, start, end)).Status
            .Should().Be(ExportDownloadStatus.RangeNotSatisfiable);
    }

    #region Harness

    /// <summary>
    /// A completed export whose stored archive is exactly <paramref name="length"/> bytes.
    /// </summary>
    private async Task<(Guid TransferId, HouseholdDataPortabilityService Service)> CompletedExportOfLengthAsync(long length)
    {
        var (tenantId, userId) = await SeedHouseholdAsync();
        var db = fixture.CreateDbContext();

        var transfer = new HouseholdDataTransfer
        {
            Id = Guid.NewGuid(), TenantId = tenantId,
            Kind = HouseholdDataTransferKind.Export,
            Status = HouseholdDataTransferStatus.Completed,
            RequestedByUserId = userId,
            ArchiveFileName = "famick-export-range-test.zip",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };
        db.HouseholdDataTransfers.Add(transfer);
        await db.SaveChangesAsync();

        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.GetExportArchiveInfoAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredFileInfo(length, DateTime.UtcNow));
        storage.Setup(s => s.GetExportArchiveStreamAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[length]));

        return (transfer.Id, BuildService(db, tenantId, storage.Object));
    }

    private static HouseholdDataPortabilityService BuildService(
        Infrastructure.Data.HomeManagementDbContext db,
        Guid? tenantId,
        IFileStorageService? storage = null)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(p => p.TenantId).Returns(tenantId);

        var tokens = new Mock<IFileAccessTokenService>();
        tokens.Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns("signed-token");

        return new HouseholdDataPortabilityService(
            db,
            tenantProvider.Object,
            storage ?? Mock.Of<IFileStorageService>(),
            tokens.Object,
            new HouseholdArchiveWriter(Mock.Of<IFileStorageService>(), NullLogger<HouseholdArchiveWriter>.Instance),
            NullLogger<HouseholdDataPortabilityService>.Instance);
    }

    private async Task<(Guid TenantId, Guid UserId)> SeedHouseholdAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var db = fixture.CreateDbContext();

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Household" });
        db.Users.Add(new User
        {
            Id = userId, TenantId = tenantId,
            Username = $"u-{userId:N}",
            Email = $"{userId:N}@example.com",
            FirstName = "Test", LastName = "User",
            PasswordHash = "x", IsActive = true,
        });

        await db.SaveChangesAsync();
        return (tenantId, userId);
    }

    #endregion
}
