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
        (await theirsService.OpenArchiveAsync(export.Id, null, null)).Should().BeNull();
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

        (await service.OpenArchiveAsync(transfer.Id, null, null)).Should().BeNull();
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

        var stuck = await db.HouseholdDataTransfers.SingleAsync(t => t.TenantId == tenantId);
        stuck.Status.Should().Be(HouseholdDataTransferStatus.Failed);
        stuck.ErrorCode.Should().Be("WORKER_LOST");
    }

    #region Harness

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
