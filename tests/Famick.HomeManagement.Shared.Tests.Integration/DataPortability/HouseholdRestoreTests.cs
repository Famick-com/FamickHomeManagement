using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
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
/// Round-trips a household through export and back.
/// </summary>
/// <remarks>
/// These run against real Postgres because the things most likely to break are the things an
/// in-memory provider does not model: preserved primary keys, foreign keys checked per row, and
/// timestamps that the change tracker would otherwise overwrite.
/// </remarks>
public class HouseholdRestoreTests(PostgresContainerFixture fixture) : IClassFixture<PostgresContainerFixture>
{
    [Fact]
    public async Task ARowDeletedSinceTheBackupComesBackWithItsOriginalIdAndHistory()
    {
        var world = await SeedAndExportAsync();

        // Delete a product, as somebody would by mistake.
        await using (var db = fixture.CreateDbContext())
        {
            var doomed = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId);
            db.Products.Remove(doomed);
            await db.SaveChangesAsync();
        }

        var summary = await RestoreAsync(world);

        summary.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed),
            "the restore should finish. Error was: {0}", summary.ErrorMessage);

        summary.AppliedFailed.Should().Be(0,
            "no row should fail to go back. inserted={0} updated={1} skipped={2} failed={3}",
            summary.AppliedInserted, summary.AppliedUpdated, summary.AppliedSkipped, summary.AppliedFailed);

        await using var check = fixture.CreateDbContext();
        var restored = await check.Products.IgnoreQueryFilters().SingleOrDefaultAsync(p => p.Id == world.ProductId);

        restored.Should().NotBeNull("the deleted product should be back");
        restored!.Id.Should().Be(world.ProductId, "its identity is what makes this a restore rather than a copy");
        restored.Name.Should().Be("Kitchen Scales");

        // The point of bypassing the change tracker. SaveChanges would have stamped the restore's
        // own clock over this and quietly lost the household's history.
        restored.CreatedAt.Should().BeCloseTo(world.ProductCreatedAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ARowEditedSinceTheBackupIsKeptByDefault()
    {
        var world = await SeedAndExportAsync();

        await using (var db = fixture.CreateDbContext())
        {
            var edited = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId);
            edited.Name = "Renamed After The Backup";
            edited.UpdatedAt = DateTime.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        }

        var summary = await RestoreAsync(world);

        summary.ChangedSinceCount.Should().BeGreaterThan(0, "the edit should be noticed");

        await using var check = fixture.CreateDbContext();
        var product = await check.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId);

        // Keeping the newer copy is the safe default: a restore should not silently discard work
        // done since the backup was taken.
        product.Name.Should().Be("Renamed After The Backup");
    }

    [Fact]
    public async Task ARowEditedSinceTheBackupIsOverwrittenWhenAskedFor()
    {
        var world = await SeedAndExportAsync();

        await using (var db = fixture.CreateDbContext())
        {
            var edited = await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId);
            edited.Name = "Renamed After The Backup";
            edited.UpdatedAt = DateTime.UtcNow.AddMinutes(5);
            await db.SaveChangesAsync();
        }

        var summary = await RestoreAsync(world, policy: ChangedSincePolicy.TakeBackup);

        summary.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed));

        await using var check = fixture.CreateDbContext();
        var product = await check.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId);

        product.Name.Should().Be("Kitchen Scales", "the backup's copy was asked for");
    }

    [Fact]
    public async Task RestoreNeverRemovesWhatWasCreatedAfterTheBackup()
    {
        var world = await SeedAndExportAsync();

        Guid newerId;
        await using (var db = fixture.CreateDbContext())
        {
            var newer = new Product
            {
                Id = Guid.NewGuid(), TenantId = world.TenantId, Name = "Bought Later",
                LocationId = world.LocationId,
                QuantityUnitIdPurchase = world.UnitId, QuantityUnitIdStock = world.UnitId,
            };
            db.Products.Add(newer);
            await db.SaveChangesAsync();
            newerId = newer.Id;
        }

        await RestoreAsync(world);

        await using var check = fixture.CreateDbContext();

        // Restore adds and overwrites; it does not make the household match the archive. Deleting
        // what came after would be a different and far more dangerous feature.
        (await check.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == newerId))
            .Should().BeTrue("a row created after the backup must survive a restore");
    }

    [Fact]
    public async Task RestoringTheSameArchiveTwiceChangesNothingTheSecondTime()
    {
        var world = await SeedAndExportAsync();

        await using (var db = fixture.CreateDbContext())
        {
            db.Products.Remove(await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId));
            await db.SaveChangesAsync();
        }

        await RestoreAsync(world);
        var second = await RestoreAsync(world);

        second.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed));
        second.RestoredCount.Should().Be(0, "everything is already there the second time");

        await using var check = fixture.CreateDbContext();
        (await check.Products.IgnoreQueryFilters().CountAsync(p => p.Id == world.ProductId))
            .Should().Be(1, "a preserved primary key is what makes this idempotent");
    }

    [Fact]
    public async Task AnArchiveFromAnotherHouseholdIsRefusedBeforeAnythingIsWritten()
    {
        var mine = await SeedAndExportAsync();
        var theirs = await SeedAndExportAsync();

        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, mine.TenantId, mine.Storage);

        // Their archive, my household. This single check is what lets the rest of the design
        // assume every id in an archive belongs here.
        await using var foreign = new MemoryStream(theirs.Archive);
        var started = await service.StartRestoreAsync(foreign, "theirs.zip", mine.UserId);
        await service.RunRestoreAsync(started.Id);

        var after = await db.HouseholdDataTransfers.AsNoTracking().SingleAsync(t => t.Id == started.Id);

        after.Status.Should().Be(HouseholdDataTransferStatus.Failed);
        after.ErrorCode.Should().Be("DIFFERENT_HOUSEHOLD");
        after.ErrorMessage.Should().Contain("Transfer", "the message should say what to use instead");
    }

    [Fact]
    public async Task ClassifyingWritesNothingToTheHousehold()
    {
        var world = await SeedAndExportAsync();

        await using (var db = fixture.CreateDbContext())
        {
            db.Products.Remove(await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId));
            await db.SaveChangesAsync();
        }

        await using var context = fixture.CreateDbContext();
        var service = BuildService(context, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(world.Archive);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);

        var staged = await service.GetRestoreAsync(started.Id);
        staged!.Status.Should().Be(nameof(HouseholdDataTransferStatus.AwaitingDecision));
        staged.RestoredCount.Should().BeGreaterThan(0, "it found something to put back");

        // The dry-run promise: everything above only looked.
        await using var check = fixture.CreateDbContext();
        (await check.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == world.ProductId))
            .Should().BeFalse("nothing may be written before the user confirms");
    }

    [Fact]
    public async Task ApplyIsRefusedWithoutAnExportToFallBackOn()
    {
        var world = await SeedAndExportAsync();

        // Expire the export, leaving the household with no way back from an overwrite.
        await using (var db = fixture.CreateDbContext())
        {
            var export = await db.HouseholdDataTransfers.IgnoreQueryFilters()
                .SingleAsync(t => t.TenantId == world.TenantId && t.Kind == HouseholdDataTransferKind.Export);
            export.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        await using var context = fixture.CreateDbContext();
        var service = BuildService(context, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(world.Archive);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);

        var result = await service.ApplyRestoreAsync(started.Id);

        result!.ErrorCode.Should().Be("NO_RECENT_BACKUP",
            "a restore overwrites and the only undo is an archive taken before it");
        result.Status.Should().Be(nameof(HouseholdDataTransferStatus.AwaitingDecision),
            "the restore should still be waiting rather than failed outright");
    }

    [Theory]
    // A browser sends a bare basename, but nothing stops a crafted multipart request sending
    // these. Both storage backends build a path or a key from the name they are given.
    [InlineData("../../../etc/cron.d/famick")]
    [InlineData("..\\..\\plugins\\evil.dll")]
    [InlineData("/etc/passwd")]
    [InlineData("plugins/evil.dll")]
    public async Task AnUploadedFilenameNeverReachesStorage(string hostileName)
    {
        var world = await SeedAndExportAsync();

        var namesStorageSaw = new List<string>();
        var storage = new Mock<IFileStorageService>();
        storage.Setup(s => s.SaveRestoreUploadAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, Stream _, string name, CancellationToken _) =>
            {
                namesStorageSaw.Add(name);
                return Task.FromResult(name);
            });

        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, world.TenantId, storage.Object);

        await using var stream = new MemoryStream(world.Archive);
        var started = await service.StartRestoreAsync(stream, hostileName, world.UserId);

        // The name is not sanitised, it is discarded. Sanitising would mean being certain of every
        // trick; choosing the name means there is nothing to be certain about.
        namesStorageSaw.Should().ContainSingle().Which.Should().Be("archive.zip");

        var stored = await db.HouseholdDataTransfers.AsNoTracking().SingleAsync(t => t.Id == started.Id);
        stored.UploadFileName.Should().Be("archive.zip");

        // Kept for the UI to show, with anything structural stripped out.
        stored.OriginalUploadFileName.Should().NotContain("..");
        stored.OriginalUploadFileName.Should().NotContain("/");
        stored.OriginalUploadFileName.Should().NotContain("\\");
    }

    [Fact]
    public async Task AnArchiveThatExpandsBeyondTheCeilingIsRefused()
    {
        var world = await SeedAndExportAsync();

        // A small file claiming an enormous expansion — the shape of a decompression bomb. The
        // check reads the zip's own directory, so it never has to decompress to find out.
        var bomb = BuildOversizedArchive();

        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, world.TenantId, world.Storage, TinyLimits);

        await using var stream = new MemoryStream(bomb);
        var started = await service.StartRestoreAsync(stream, "bomb.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);

        var after = await db.HouseholdDataTransfers.AsNoTracking().SingleAsync(t => t.Id == started.Id);
        after.Status.Should().Be(HouseholdDataTransferStatus.Failed);
        after.ErrorCode.Should().Be("ARCHIVE_TOO_LARGE");
    }

    /// <summary>
    /// An archive whose entries expand past the reader's ceiling. Highly compressible so the file
    /// itself stays small — which is the whole trick.
    /// </summary>
    private static byte[] BuildOversizedArchive()
    {
        var buffer = new MemoryStream();

        using (var zip = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var manifest = zip.CreateEntry("manifest.json");
            using (var stream = manifest.Open())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write("""{"schemaVersion":1,"minimumReaderVersion":1,"tables":[],"source":{"mode":"selfHosted"}}""");
            }

            // Two megabytes of zeroes, against the one-megabyte ceiling this test configures.
            // Proving the check works does not require building something the size of the real
            // limit — deflating nine gigabytes on every run costs CI minutes and shows nothing
            // the proportions here do not.
            var padding = zip.CreateEntry("data/000.padding.jsonl", System.IO.Compression.CompressionLevel.Optimal);
            using var padStream = padding.Open();

            var chunk = new byte[64 * 1024];
            for (var written = 0L; written < 2 * 1024 * 1024; written += chunk.Length)
                padStream.Write(chunk);
        }

        return buffer.ToArray();
    }

    [Fact]
    public async Task ADeletedAttachmentComesBackWithItsBytes()
    {
        var world = await SeedAndExportAsync(withProductImage: true);

        // Delete the image row, as deleting the product's photo would.
        await using (var db = fixture.CreateDbContext())
        {
            var image = await db.ProductImages.IgnoreQueryFilters().SingleAsync(i => i.TenantId == world.TenantId);
            db.ProductImages.Remove(image);
            await db.SaveChangesAsync();
        }

        var summary = await RestoreAsync(world);
        summary.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed));

        await using var check = fixture.CreateDbContext();
        var restored = await check.ProductImages.IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.TenantId == world.TenantId);

        restored.Should().NotBeNull("the image row should be back");

        // Under a name storage chose, not the one the archive supplied — the Save methods keep
        // only the extension. That is the property that stops a hostile archive deciding where
        // its bytes land, so the row is rewritten to match rather than the naming worked around.
        restored!.FileName.Should().NotBeNullOrEmpty();
        world.SavedFiles.Should().ContainKey(restored.FileName,
            "the row must point at a file that was actually written");
        world.SavedFiles[restored.FileName].Should().Equal("photo bytes"u8.ToArray());
    }

    [Fact]
    public async Task AnAttachmentThatFailsItsChecksumIsRefusedRatherThanWritten()
    {
        var world = await SeedAndExportAsync(withProductImage: true);
        var tampered = TamperWithArchivedFile(world.Archive);

        await using (var db = fixture.CreateDbContext())
        {
            var image = await db.ProductImages.IgnoreQueryFilters().SingleAsync(i => i.TenantId == world.TenantId);
            db.ProductImages.Remove(image);
            await db.SaveChangesAsync();
        }

        await using var db2 = fixture.CreateDbContext();
        var service = BuildService(db2, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(tampered);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);
        var result = await service.ApplyRestoreAsync(started.Id);

        result!.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed),
            "one bad attachment must not cost the household the rest of the restore");

        await using var check = fixture.CreateDbContext();
        var restored = await check.ProductImages.IgnoreQueryFilters()
            .SingleOrDefaultAsync(i => i.TenantId == world.TenantId);

        // ProductImage.FileName is not nullable — a product image with no image is nothing at
        // all — so the row is dropped rather than inserted pointing at a file that was refused.
        // A contact, whose photo column is nullable, would come back without its picture instead.
        restored.Should().BeNull("a row whose only purpose is a refused file should not be written");
    }

    /// <summary>
    /// Rewrites the bytes of the archived image, leaving its manifest checksum stale.
    /// </summary>
    private static byte[] TamperWithArchivedFile(byte[] archive)
    {
        var copy = new MemoryStream();
        copy.Write(archive);
        copy.Position = 0;

        using (var zip = new System.IO.Compression.ZipArchive(copy, System.IO.Compression.ZipArchiveMode.Update, true))
        {
            var entry = zip.Entries.First(e => e.FullName.StartsWith("files/", StringComparison.Ordinal));
            var name = entry.FullName;
            entry.Delete();

            var replacement = zip.CreateEntry(name);
            using var stream = replacement.Open();
            stream.Write("not the original bytes"u8);
        }

        return copy.ToArray();
    }

    [Fact]
    public async Task OneRowThatWillNotGoBackDoesNotTakeTheRestoreWithIt()
    {
        var world = await SeedAndExportAsync();

        // A row referencing a location that is not in the archive and not in the household. Its
        // insert fails on the foreign key — which is the case the applier says it tolerates.
        var broken = InjectUnsatisfiableRow(world.Archive);

        await using (var db = fixture.CreateDbContext())
        {
            db.Products.Remove(await db.Products.IgnoreQueryFilters().SingleAsync(p => p.Id == world.ProductId));
            await db.SaveChangesAsync();
        }

        await using var db2 = fixture.CreateDbContext();
        var service = BuildService(db2, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(broken);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);
        var result = await service.ApplyRestoreAsync(started.Id);

        // PostgreSQL aborts the whole transaction on a statement error and refuses every command
        // after it. Catching the exception is not enough on its own — without a savepoint per row
        // the first bad row takes everything after it, and the restore reports failures it never
        // actually attempted.
        result!.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed),
            "the restore should finish. Error was: {0}", result.ErrorMessage);
        result.AppliedFailed.Should().Be(1, "exactly the one bad row should fail");

        await using var check = fixture.CreateDbContext();
        (await check.Products.IgnoreQueryFilters().AnyAsync(p => p.Id == world.ProductId))
            .Should().BeTrue("the good rows must still go back");
    }

    /// <summary>
    /// Adds a product row pointing at a location that exists nowhere, so its insert fails.
    /// </summary>
    private static byte[] InjectUnsatisfiableRow(byte[] archive)
    {
        var copy = new MemoryStream();
        copy.Write(archive);
        copy.Position = 0;

        using (var zip = new System.IO.Compression.ZipArchive(copy, System.IO.Compression.ZipArchiveMode.Update, true))
        {
            var entry = zip.Entries.First(e => e.FullName.Contains(".products.", StringComparison.Ordinal));

            string existing;
            using (var reader = new StreamReader(entry.Open())) existing = reader.ReadToEnd();

            var orphan = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["Id"] = Guid.NewGuid(),
                ["TenantId"] = Guid.NewGuid(),
                ["Name"] = "Points At Nothing",
                ["LocationId"] = Guid.NewGuid(),
                ["QuantityUnitIdPurchase"] = Guid.NewGuid(),
                ["QuantityUnitIdStock"] = Guid.NewGuid(),
                ["CreatedAt"] = DateTime.UtcNow,
            });

            entry.Delete();
            var replacement = zip.CreateEntry(entry.FullName);
            using var stream = replacement.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(existing);
            writer.Write(orphan);
            writer.Write('\n');
        }

        return copy.ToArray();
    }

    [Fact]
    public async Task AnAttachmentTheManifestDoesNotDescribeIsRefused()
    {
        var world = await SeedAndExportAsync(withProductImage: true);

        // The bytes are left exactly as exported; only the manifest entry describing them is
        // removed. Tampering as well would let the checksum test pass this one on its own, and
        // the point here is narrower: a file the manifest does not describe is refused even when
        // there is nothing wrong with it. Treating a missing entry as "nothing to check against"
        // would make the checksum decorative — it would only ever run on untouched files.
        var stripped = RemoveManifestFileEntries(world.Archive);

        await using (var db = fixture.CreateDbContext())
        {
            var image = await db.ProductImages.IgnoreQueryFilters().SingleAsync(i => i.TenantId == world.TenantId);
            db.ProductImages.Remove(image);
            await db.SaveChangesAsync();
        }

        await using var db2 = fixture.CreateDbContext();
        var service = BuildService(db2, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(stripped);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);
        await service.ApplyRestoreAsync(started.Id);

        world.SavedFiles.Should().BeEmpty("a file the manifest does not describe must not be written");
    }

    [Fact]
    public async Task ADuplicateManifestEntryIsRefusedRatherThanThrowingLater()
    {
        var world = await SeedAndExportAsync(withProductImage: true);
        var duplicated = DuplicateFirstManifestFileEntry(world.Archive);

        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(duplicated);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);

        var after = await db.HouseholdDataTransfers.AsNoTracking().SingleAsync(t => t.Id == started.Id);

        // Refused when the archive is opened, with the reason. Two entries claiming one path is
        // not something to reconcile — picking one means choosing which checksum to believe.
        after.Status.Should().Be(HouseholdDataTransferStatus.Failed);
        after.ErrorCode.Should().Be("UNREADABLE");
    }

    private static byte[] RemoveManifestFileEntries(byte[] archive) =>
        RewriteManifest(archive, manifest =>
        {
            manifest["Files"] = System.Text.Json.JsonSerializer.SerializeToElement(Array.Empty<object>());
            return manifest;
        });

    private static byte[] DuplicateFirstManifestFileEntry(byte[] archive) =>
        RewriteManifest(archive, manifest =>
        {
            var files = manifest["Files"].EnumerateArray().Select(e => e.Clone()).ToList();
            files.Add(files[0]);
            manifest["Files"] = System.Text.Json.JsonSerializer.SerializeToElement(files);
            return manifest;
        });

    private static byte[] RewriteManifest(
        byte[] archive,
        Func<Dictionary<string, System.Text.Json.JsonElement>, Dictionary<string, System.Text.Json.JsonElement>> edit)
    {
        var copy = new MemoryStream();
        copy.Write(archive);
        copy.Position = 0;

        using (var zip = new System.IO.Compression.ZipArchive(copy, System.IO.Compression.ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("manifest.json")!;

            Dictionary<string, System.Text.Json.JsonElement> manifest;
            using (var read = entry.Open())
                manifest = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(read)!;

            entry.Delete();

            var replacement = zip.CreateEntry("manifest.json");
            using var write = replacement.Open();
            System.Text.Json.JsonSerializer.Serialize(write, edit(manifest));
        }

        return copy.ToArray();
    }

    [Fact]
    public async Task AFileCarryingTableSpanningSeveralBatchesRestoresEveryRow()
    {
        // Small batches, so a handful of rows spans several of them. With one batch the bug this
        // covers cannot appear: restoring an attachment used to seek the same stream the table
        // enumeration was suspended on, so only the rows after the first batch were read from the
        // wrong place.
        var limits = ArchiveReaderLimits.Default with { BatchSize = 2 };

        var world = await SeedAndExportAsync(withProductImage: true, extraImages: 7);

        await using (var db = fixture.CreateDbContext())
        {
            db.Products.RemoveRange(await db.Products.IgnoreQueryFilters()
                .Where(p => p.TenantId == world.TenantId).ToListAsync());
            db.ProductImages.RemoveRange(await db.ProductImages.IgnoreQueryFilters()
                .Where(i => i.TenantId == world.TenantId).ToListAsync());
            await db.SaveChangesAsync();
        }

        await using var db2 = fixture.CreateDbContext();
        var service = BuildService(db2, world.TenantId, world.Storage, limits);

        await using var stream = new MemoryStream(world.Archive);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);
        var result = await service.ApplyRestoreAsync(started.Id);

        result!.Status.Should().Be(nameof(HouseholdDataTransferStatus.Completed),
            "Error was: {0}", result.ErrorMessage);

        await using var check = fixture.CreateDbContext();

        // ProductImage is the table that both carries files and spans batches — restoring an
        // attachment mid-enumeration is exactly the case that used to lose the rows after it.
        (await check.ProductImages.IgnoreQueryFilters().CountAsync(i => i.TenantId == world.TenantId))
            .Should().Be(8, "every image should come back, not only those in the first batch");
    }

    #region Harness

    private sealed record World(
        Guid TenantId, Guid UserId, Guid ProductId, Guid LocationId, Guid UnitId,
        DateTime ProductCreatedAt, byte[] Archive, IFileStorageService Storage,
        Dictionary<string, byte[]> SavedFiles);

    /// <summary>
    /// A household with something in it, exported, with the archive captured.
    /// </summary>
    private async Task<World> SeedAndExportAsync(bool withProductImage = false, int extraImages = 0)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-30);

        Guid productId, locationId, unitId;

        await using (var db = fixture.CreateDbContext())
        {
            db.Tenants.Add(new Tenant { Id = tenantId, Name = "The Therien Family" });
            db.Users.Add(new User
            {
                Id = userId, TenantId = tenantId, Username = $"u-{userId:N}",
                Email = $"{userId:N}@example.com", FirstName = "Test", LastName = "User",
                PasswordHash = "x", IsActive = true,
            });

            var location = new Location { Id = Guid.NewGuid(), TenantId = tenantId, Name = "Pantry" };
            var unit = new QuantityUnit
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Piece", NamePlural = "Pieces",
            };
            var product = new Product
            {
                Id = Guid.NewGuid(), TenantId = tenantId, Name = "Kitchen Scales",
                LocationId = location.Id,
                QuantityUnitIdPurchase = unit.Id, QuantityUnitIdStock = unit.Id,
                CreatedAt = createdAt, UpdatedAt = createdAt,
            };

            db.AddRange(location, unit, product);
            await db.SaveChangesAsync();

            // SaveChanges stamps CreatedAt, so put the intended history back underneath it.
            // Through EF rather than raw SQL: column naming is mixed across this model — some
            // entities are snake_cased and some keep the CLR name — so hand-written column names
            // are wrong about half the time.
            await db.Products.Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(p => p.CreatedAt, createdAt)
                    .SetProperty(p => p.UpdatedAt, createdAt));

            if (withProductImage)
            {
                db.ProductImages.Add(new ProductImage
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id,
                    FileName = "image_20260101_000000_abcd1234.jpg",
                    OriginalFileName = "scales.jpg",
                    ContentType = "image/jpeg", FileSize = 11,
                });

                for (var i = 0; i < extraImages; i++)
                {
                    db.ProductImages.Add(new ProductImage
                    {
                        Id = Guid.NewGuid(), TenantId = tenantId, ProductId = product.Id,
                        FileName = $"image_20260101_00000{i}_abcd123{i}.jpg",
                        OriginalFileName = $"photo{i}.jpg",
                        ContentType = "image/jpeg", FileSize = 11,
                    });
                }

                await db.SaveChangesAsync();
            }

            productId = product.Id; locationId = location.Id; unitId = unit.Id;
        }

        var archives = new Dictionary<Guid, byte[]>();
        var savedFiles = new Dictionary<string, byte[]>();

        var storage = new Mock<IFileStorageService>();

        // Serves the image when exporting, and records it when restoring. The Save method
        // generates the stored name, exactly as the real implementations do, so the test sees the
        // same rename the product does.
        storage.Setup(s => s.GetProductImageStreamAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new MemoryStream("photo bytes"u8.ToArray()));
        storage.Setup(s => s.SaveProductImageAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid _, Stream content, string original, CancellationToken token) =>
            {
                using var buffer = new MemoryStream();
                await content.CopyToAsync(buffer, token);

                var generated = $"image_{Guid.NewGuid():N}{Path.GetExtension(original)}";
                savedFiles[generated] = buffer.ToArray();
                return generated;
            });
        storage.Setup(s => s.SaveExportArchiveAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, Stream stream, string name, CancellationToken token) =>
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy, token);
                archives[id] = copy.ToArray();
                return name;
            });
        storage.Setup(s => s.SaveRestoreUploadAsync(It.IsAny<Guid>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, Stream stream, string name, CancellationToken token) =>
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy, token);
                archives[id] = copy.ToArray();
                return name;
            });
        storage.Setup(s => s.GetRestoreUploadStreamAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string _, CancellationToken _) => new MemoryStream(archives[id]));

        await using var exportDb = fixture.CreateDbContext();
        var exporter = BuildService(exportDb, tenantId, storage.Object);

        var export = await exporter.StartExportAsync(new StartExportRequest { IncludeFiles = withProductImage }, userId);
        await exporter.RunExportAsync(export.Id);

        return new World(tenantId, userId, productId, locationId, unitId, createdAt,
            archives[export.Id], storage.Object, savedFiles);
    }

    private async Task<RestoreSummary> RestoreAsync(World world, ChangedSincePolicy? policy = null)
    {
        await using var db = fixture.CreateDbContext();
        var service = BuildService(db, world.TenantId, world.Storage);

        await using var stream = new MemoryStream(world.Archive);
        var started = await service.StartRestoreAsync(stream, "backup.zip", world.UserId);
        await service.RunRestoreAsync(started.Id);

        if (policy.HasValue)
        {
            await service.SetRestoreDecisionsAsync(started.Id,
                new RestoreDecisionsRequest { ChangedSincePolicy = policy.Value.ToString() });
        }

        return await service.ApplyRestoreAsync(started.Id) ?? await service.GetRestoreAsync(started.Id)
            ?? throw new InvalidOperationException("The restore vanished.");
    }

    /// <summary>
    /// Small enough to trip with a small archive. The production numbers are in
    /// <see cref="ArchiveReaderLimits.Default"/>; reaching them in a test means deflating
    /// gigabytes on every run for no extra confidence.
    /// </summary>
    private static readonly ArchiveReaderLimits TinyLimits = new(
        MaxUncompressedBytes: 1024 * 1024,
        MaxRowsPerTable: 1_000,
        MaxRowBytes: 64 * 1024,
        BatchSize: 100);

    private static HouseholdDataPortabilityService BuildService(
        HomeManagementDbContext db, Guid tenantId, IFileStorageService storage,
        ArchiveReaderLimits? limits = null)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(p => p.TenantId).Returns(tenantId);

        var tokens = new Mock<IFileAccessTokenService>();
        tokens.Setup(t => t.GenerateToken(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns("signed-token");

        return new HouseholdDataPortabilityService(
            db, tenantProvider.Object, storage, tokens.Object,
            new HouseholdArchiveWriter(storage, NullLogger<HouseholdArchiveWriter>.Instance),
            new HouseholdArchiveReader(limits ?? ArchiveReaderLimits.Default),
            new RestoreClassifier(),
            new HouseholdRestoreApplier(NullLogger<HouseholdRestoreApplier>.Instance),
            NullLogger<HouseholdDataPortabilityService>.Instance);
    }

    #endregion
}
