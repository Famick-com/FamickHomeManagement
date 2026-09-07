using System.Data;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// The restore half: putting a household's own archive back.
/// </summary>
/// <remarks>
/// Deliberately narrow. It accepts an archive whose <c>source.tenantId</c> is this household and
/// nothing else, which is what removes every hard problem the general case has: every id in the
/// archive was this household's when it was written, so matching is exact, there is no
/// cross-household collision to resolve and no user to map. Moving data between households or
/// deployments is Transfer's job.
/// </remarks>
public sealed partial class HouseholdDataPortabilityService
{
    public async Task<RestoreSummary> StartRestoreAsync(
        Stream archive, string fileName, Guid requestedByUserId, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await ReconcileAbandonedRunsAsync(tenantId, ct);

        var transfer = new HouseholdDataTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = HouseholdDataTransferKind.Restore,
            Status = HouseholdDataTransferStatus.Queued,
            RequestedByUserId = requestedByUserId,
            UploadFileName = StoredUploadName,
            OriginalUploadFileName = DisplayNameFor(fileName),
            UploadBytes = archive.CanSeek ? archive.Length : null,
        };

        context.HouseholdDataTransfers.Add(transfer);
        await context.SaveChangesAsync(ct);

        // Stored under a name this code chose, never the one that arrived. The uploaded name is
        // attacker-controlled — a browser sends a bare basename but nothing stops a crafted
        // multipart request sending "../../plugins/evil.dll" — and both storage backends build a
        // path or key from it. The local one hands it to Path.Combine, which happily walks out of
        // the uploads directory; the plugin loader reads DLLs from a sibling of it. Sanitising
        // would mean being sure of every trick, so the name is simply not used.
        await storage.SaveRestoreUploadAsync(transfer.Id, archive, StoredUploadName, ct);

        logger.LogInformation("Queued restore {TransferId} for tenant {TenantId}", transfer.Id, tenantId);
        return await ToRestoreSummaryAsync(transfer, ct);
    }

    public async Task<RestoreSummary?> GetRestoreAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        return transfer == null ? null : await ToRestoreSummaryAsync(transfer, ct);
    }

    public async Task<bool> RunRestoreAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await context.HouseholdDataTransfers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

        if (transfer == null || transfer.Status != HouseholdDataTransferStatus.Queued) return false;

        var tenantId = transfer.TenantId;
        tenantProvider.SetTenantId(tenantId);

        // Claimed by a conditional update, not by reading and then writing. More than one worker
        // can select the same queued row, and both would otherwise start classifying it — the
        // unique index on the item rows then rejects the second one's work and reports a failure
        // that was really a collision. Whoever changes the row owns the run.
        var claimed = await context.HouseholdDataTransfers
            .Where(t => t.Id == transferId && t.Status == HouseholdDataTransferStatus.Queued)
            .ExecuteUpdateAsync(set => set
                .SetProperty(t => t.Status, HouseholdDataTransferStatus.Running)
                .SetProperty(t => t.StartedAt, DateTime.UtcNow)
                .SetProperty(t => t.HeartbeatAt, DateTime.UtcNow), ct);

        if (claimed == 0) return false;

        await context.Entry(transfer).ReloadAsync(ct);

        try
        {
            await ClassifyAsync(transfer, tenantId, ct);

            transfer.Status = HouseholdDataTransferStatus.AwaitingDecision;
            transfer.HeartbeatAt = DateTime.UtcNow;
            await context.SaveChangesAsync(ct);
        }
        catch (RestoreRefusedException ex)
        {
            transfer.Status = HouseholdDataTransferStatus.Failed;
            transfer.ErrorCode = ex.Code;
            transfer.ErrorMessage = ex.Message;
            transfer.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore {TransferId} failed while reading the archive", transferId);

            transfer.Status = HouseholdDataTransferStatus.Failed;
            transfer.ErrorCode = "RESTORE_FAILED";
            transfer.ErrorMessage = ex.Message;
            transfer.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return true;
    }

    /// <summary>
    /// Reads the archive and records what each row would do. Writes nothing to the household.
    /// </summary>
    private async Task ClassifyAsync(HouseholdDataTransfer transfer, Guid tenantId, CancellationToken ct)
    {
        var upload = await storage.GetRestoreUploadStreamAsync(transfer.Id, transfer.UploadFileName!, ct)
            ?? throw new RestoreRefusedException("UPLOAD_MISSING", "The uploaded archive could not be read.");

        await using var archive = await RestoreArchive.BufferAsync(upload, ct);

        await using var inspect = archive.OpenRead();
        var opened = reader.Open(inspect, tenantId);

        if (!opened.IsUsable)
        {
            throw opened.Rejection switch
            {
                ArchiveRejection.DifferentHousehold => new RestoreRefusedException(
                    "DIFFERENT_HOUSEHOLD",
                    "This archive belongs to a different household, so it cannot be restored here. " +
                    "Use Transfer to move data between servers."),
                ArchiveRejection.TooNew => new RestoreRefusedException(
                    "ARCHIVE_TOO_NEW",
                    "This archive was made by a newer version of Famick than this server understands."),
                ArchiveRejection.TooLarge => new RestoreRefusedException(
                    "ARCHIVE_TOO_LARGE",
                    "This archive expands to more than a restore will read."),
                _ => new RestoreRefusedException("UNREADABLE", "This file is not a Famick export archive."),
            };
        }

        var manifest = opened.Manifest!;
        transfer.ManifestJson = JsonSerializer.Serialize(manifest);

        var connectionString = context.Database.GetConnectionString()!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var byName = context.Model.GetEntityTypes().ToDictionary(t => t.ClrType.Name, t => t);
        var items = new List<HouseholdDataTransferItem>();
        var counts = new Dictionary<RestoreClassification, int>();
        var table = 0;

        foreach (var described in manifest.Tables)
        {
            ct.ThrowIfCancellationRequested();
            table++;

            if (!byName.TryGetValue(described.Entity, out var entityType)) continue;
            if (ExportRegistry.For(entityType.ClrType).Import == ImportPolicy.Never) continue;

            await ReportProgressAsync(transfer, described.Entity, table, manifest.Tables.Count, ct);

            // A batch at a time, not a table at a time. A table is allowed to be large, and
            // holding all of it — plus a lookup keyed by every id in it — is what turns a
            // permitted archive into an exhausted worker.
            await foreach (var batch in ReadInBatchesAsync(archive, described, ct))
            {
                var existing = await classifier.FindExistingAsync(
                    connection, entityType, batch.Select(r => r.Id).ToList(), ct);

                foreach (var row in batch)
                {
                    existing.TryGetValue(row.Id, out var match);

                    var classification = classifier.Classify(row, match, tenantId, out var source, out var target);
                    counts[classification] = counts.GetValueOrDefault(classification) + 1;

                    // Only rows that need a decision or report a problem are stored. A restore is
                    // mostly rows that match and have nothing to say; one bookkeeping row each would
                    // make the bookkeeping larger than the data.
                    if (classification is RestoreClassification.ChangedSince or RestoreClassification.Invalid)
                    {
                        items.Add(new HouseholdDataTransferItem
                        {
                            Id = Guid.NewGuid(),
                            TenantId = tenantId,
                            TransferId = transfer.Id,
                            Category = described.Entity,
                            SourceId = row.Id,
                            Label = RestoreClassifier.LabelFor(row),
                            Classification = classification,
                            SourceUpdatedAt = source,
                            TargetUpdatedAt = target,
                            Status = RestoreItemStatus.Pending,
                        });
                    }
                }

                // Flushed per batch so the item rows do not accumulate either.
                if (items.Count >= 500)
                {
                    context.HouseholdDataTransferItems.AddRange(items);
                    await context.SaveChangesAsync(ct);
                    items.Clear();
                }
            }
        }

        if (items.Count > 0)
        {
            context.HouseholdDataTransferItems.AddRange(items);
            await context.SaveChangesAsync(ct);
        }

        transfer.ProgressLabel = null;
        transfer.ProgressCurrent = counts.Values.Sum();
        transfer.ProgressTotal = counts.Values.Sum();
        transfer.RestoreCountsJson = JsonSerializer.Serialize(
            counts.ToDictionary(c => c.Key.ToString(), c => c.Value));
    }

    public async Task<RestoreReport?> GetRestoreReportAsync(
        Guid transferId, string? category, int skip, int take, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        if (transfer == null) return null;

        var query = context.HouseholdDataTransferItems
            .AsNoTracking()
            .Where(i => i.TransferId == transferId && i.Classification == RestoreClassification.ChangedSince);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(i => i.Category == category);

        var total = await query.CountAsync(ct);

        var conflicts = await query
            .OrderBy(i => i.Category).ThenBy(i => i.Label)
            .Skip(skip).Take(Math.Clamp(take, 1, 200))
            .Select(i => new RestoreConflict
            {
                ItemId = i.Id,
                Category = i.Category,
                SourceId = i.SourceId,
                Label = i.Label,
                BackupUpdatedAt = i.SourceUpdatedAt,
                CurrentUpdatedAt = i.TargetUpdatedAt,
                Decision = i.Decision == null ? null : i.Decision.ToString(),
            })
            .ToListAsync(ct);

        var counts = ReadCounts(transfer);

        return new RestoreReport
        {
            Conflicts = conflicts,
            TotalConflicts = total,
            Categories = await query.GroupBy(i => i.Category)
                .Select(g => new RestoreCategorySummary { Category = g.Key, ChangedSince = g.Count() })
                .ToListAsync(ct),
        };
    }

    public async Task<bool> SetRestoreDecisionsAsync(
        Guid transferId, RestoreDecisionsRequest decisions, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        if (transfer?.Status != HouseholdDataTransferStatus.AwaitingDecision) return false;

        if (Enum.TryParse<ChangedSincePolicy>(decisions.ChangedSincePolicy, out var policy))
            transfer.ChangedSincePolicy = policy;

        foreach (var over in decisions.Overrides)
        {
            if (!Enum.TryParse<ChangedSincePolicy>(over.Decision, out var decision)) continue;

            var item = await context.HouseholdDataTransferItems
                .FirstOrDefaultAsync(i => i.Id == over.ItemId && i.TransferId == transferId, ct);

            if (item != null) item.Decision = decision;
        }

        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelRestoreAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        if (transfer == null) return false;

        // Only where it means something. Cancelling a finished restore used to overwrite its
        // status and delete the upload, so the results screen lost the counts and the reason for
        // them — for a restore that had already happened and could not be taken back.
        if (transfer.Status is not (HouseholdDataTransferStatus.Queued
                               or HouseholdDataTransferStatus.Running
                               or HouseholdDataTransferStatus.AwaitingDecision))
        {
            return false;
        }

        if (transfer.UploadFileName != null)
            await storage.DeleteRestoreUploadAsync(transferId, transfer.UploadFileName, ct);

        transfer.Status = HouseholdDataTransferStatus.Cancelled;
        transfer.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<RestoreSummary?> ApplyRestoreAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        if (transfer?.Status != HouseholdDataTransferStatus.AwaitingDecision) return null;

        var tenantId = transfer.TenantId;

        // The undo. A restore overwrites rows and there is no way back except an archive taken
        // before it, so one has to exist — and be current enough to still be downloadable.
        if (!await HasRecentBackupAsync(tenantId, ct))
        {
            transfer.ErrorCode = "NO_RECENT_BACKUP";
            transfer.ErrorMessage = "Take an export of this household before restoring over it.";
            await context.SaveChangesAsync(ct);
            return await ToRestoreSummaryAsync(transfer, ct);
        }

        // Same again, and it matters more here: two concurrent applies would each write the
        // household's rows, in separate transactions, from the same archive. Only the request
        // that moves the row off AwaitingDecision does the writing.
        var claimed = await context.HouseholdDataTransfers
            .Where(t => t.Id == transferId && t.Status == HouseholdDataTransferStatus.AwaitingDecision)
            .ExecuteUpdateAsync(set => set
                .SetProperty(t => t.Status, HouseholdDataTransferStatus.Applying)
                .SetProperty(t => t.HeartbeatAt, DateTime.UtcNow)
                .SetProperty(t => t.ErrorCode, (string?)null)
                .SetProperty(t => t.ErrorMessage, (string?)null), ct);

        if (claimed == 0) return null;

        await context.Entry(transfer).ReloadAsync(ct);

        try
        {
            var outcome = await WriteRestoreAsync(transfer, tenantId, ct);

            // Merged, not replaced. What the restore found and what it then did are different
            // questions, and the results screen asks both — overwriting the classification counts
            // here left it reporting that nothing had been found to put back.
            var counts = ReadCounts(transfer);
            foreach (var (key, value) in outcome) counts[key] = value;

            transfer.Status = HouseholdDataTransferStatus.Completed;
            transfer.CompletedAt = DateTime.UtcNow;
            transfer.RestoreCountsJson = JsonSerializer.Serialize(counts);

            await context.SaveChangesAsync(ct);

            if (transfer.UploadFileName != null)
                await storage.DeleteRestoreUploadAsync(transferId, transfer.UploadFileName, CancellationToken.None);

            logger.LogInformation("Restore {TransferId} applied", transferId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Restore {TransferId} failed while writing", transferId);

            transfer.Status = HouseholdDataTransferStatus.Failed;
            transfer.ErrorCode = "RESTORE_APPLY_FAILED";
            transfer.ErrorMessage = ex.Message;
            transfer.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }

        return await ToRestoreSummaryAsync(transfer, ct);
    }

    /// <summary>
    /// Writes every table in one transaction.
    /// </summary>
    /// <remarks>
    /// All of it or none. A half-applied restore leaves rows pointing at parents that were never
    /// written, and no screen could explain the result to the person looking at it.
    /// </remarks>
    private async Task<Dictionary<string, int>> WriteRestoreAsync(
        HouseholdDataTransfer transfer, Guid tenantId, CancellationToken ct)
    {
        var manifest = JsonSerializer.Deserialize<ArchiveManifest>(
            transfer.ManifestJson!, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        var upload = await storage.GetRestoreUploadStreamAsync(transfer.Id, transfer.UploadFileName!, ct)
            ?? throw new RestoreRefusedException("UPLOAD_MISSING", "The uploaded archive is no longer available.");

        await using var archive = await RestoreArchive.BufferAsync(upload, ct);

        var overrides = await context.HouseholdDataTransferItems
            .AsNoTracking()
            .Where(i => i.TransferId == transfer.Id && i.Decision != null)
            .ToDictionaryAsync(i => (i.Category, i.SourceId), i => i.Decision!.Value, ct);

        var byName = context.Model.GetEntityTypes().ToDictionary(t => t.ClrType.Name, t => t);
        var inScope = manifest.Tables
            .Select(t => byName.TryGetValue(t.Entity, out var e) ? e : null)
            .Where(e => e != null).Select(e => e!)
            .ToHashSet();

        var connectionString = context.Database.GetConnectionString()!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        var totals = new Dictionary<string, int>
        {
            ["Inserted"] = 0,
            ["Updated"] = 0,
            ["Skipped"] = 0,
            ["Failed"] = 0,
        };

        // Files are written before the rows that reference them, and storage has no part in the
        // database transaction. So if the transaction never commits, every file this restore put
        // there is left behind with nothing pointing at it. Tolerating one orphan from a single
        // failed row is one thing; leaving a whole restore's worth is another, and it is
        // avoidable by remembering what was written.
        var writtenFiles = new List<(ArchiveFileSource Source, string StoredAs)>();

        // The manifest is what says how big each file should be and what it hashes to, so a
        // restored file can be checked against what the export recorded rather than trusted.
        var archivedFiles = manifest.Files.ToDictionary(f => f.Path, StringComparer.Ordinal);

        // Kept so the second pass can fill in the references left null on the way in.
        var insertedByType = new Dictionary<IEntityType, List<StagedRow>>();
        var deferredByType = new Dictionary<IEntityType, IReadOnlySet<string>>();

        try
        {
            var index = 0;
            foreach (var described in manifest.Tables)
            {
                ct.ThrowIfCancellationRequested();
                index++;

                if (!byName.TryGetValue(described.Entity, out var entityType)) continue;
                if (ExportRegistry.For(entityType.ClrType).Import == ImportPolicy.Never) continue;

                await ReportProgressAsync(transfer, described.Entity, index, manifest.Tables.Count, ct);

                var deferred = HouseholdRestoreApplier.DeferredColumnsFor(entityType, inScope);
                deferredByType[entityType] = deferred;

                await foreach (var batch in ReadInBatchesAsync(archive, described, ct))
                {
                    var existing = await classifier.FindExistingAsync(
                        connection, entityType, batch.Select(r => r.Id).ToList(), ct);

                    var work = new List<(StagedRow Row, RestoreClassification Classification, bool Overwrite)>();
                    var inserted = new List<StagedRow>();

                    foreach (var row in batch)
                    {
                        existing.TryGetValue(row.Id, out var match);
                        var classification = classifier.Classify(row, match, tenantId, out _, out _);

                        var overwrite = overrides.TryGetValue((described.Entity, row.Id), out var decision)
                            ? decision == ChangedSincePolicy.TakeBackup
                            : transfer.ChangedSincePolicy == ChangedSincePolicy.TakeBackup;

                        work.Add((row, classification, overwrite));
                        if (classification == RestoreClassification.Restored) inserted.Add(row);
                    }

                    // Files before rows. A row inserted first would, for as long as the copy took, point
                    // at bytes that were not there — and if the copy then failed, permanently. The
                    // reverse order leaves an orphaned blob, which is waste rather than breakage.
                    //
                    // Rewrites each row to the name storage chose. The Save methods generate their own,
                    // keeping only the extension, and that is worth having rather than working around: a
                    // restored file ends up under a name this application picked, so a hostile archive
                    // cannot decide where its bytes land or what they are called.
                    if (ArchiveFileSources.CarriesFiles(described.Entity))
                        work = await RestoreFilesForAsync(archive, entityType, work, archivedFiles, writtenFiles, totals, ct);

                    var outcome = await applier.ApplyTableAsync(
                        connection, transaction, entityType, tenantId, work, deferred, ct);

                    totals["Inserted"] += outcome.Inserted;
                    totals["Updated"] += outcome.Updated;
                    totals["Skipped"] += outcome.Skipped;
                    totals["Failed"] += outcome.Failed;

                    if (inserted.Count > 0)
                    {
                        if (insertedByType.TryGetValue(entityType, out var already)) already.AddRange(inserted);
                        else insertedByType[entityType] = inserted;
                    }
                }
            }

            // Second pass, now that every table is present: the cycles and self-references that could
            // not be written on the way in.
            foreach (var (entityType, rows) in insertedByType)
            {
                await applier.PatchDeferredReferencesAsync(
                    connection, transaction, entityType, rows, deferredByType[entityType], ct);
            }

            await transaction.CommitAsync(ct);
            return totals;
        }
        catch
        {
            // The transaction unwinds itself; the files do not.
            await DeleteWrittenFilesAsync(writtenFiles, CancellationToken.None);
            throw;
        }
    }

    /// <summary>
    /// Reads a table in fixed-size batches, so the working set does not follow the archive's size.
    /// </summary>
    private async IAsyncEnumerable<List<StagedRow>> ReadInBatchesAsync(
        RestoreArchive archive, ArchiveTable table,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Its own handle, held for the length of this enumeration. Nothing else may move it.
        await using var stream = archive.OpenRead();

        var size = ArchiveReaderLimits.Default.BatchSize;
        var batch = new List<StagedRow>(size);

        await foreach (var row in reader.ReadTableAsync(stream, table, ct))
        {
            batch.Add(row);
            if (batch.Count < size) continue;

            yield return batch;
            batch = new List<StagedRow>(size);
        }

        if (batch.Count > 0) yield return batch;
    }

    /// <summary>
    /// Puts a table's attachments back, and repoints its rows at where they actually landed.
    /// </summary>
    /// <remarks>
    /// Only for rows being inserted. A row that already exists here keeps the file it already
    /// has: its attachment was not what the restore was asked to change, and replacing it would
    /// discard something the archive has no better claim to.
    /// </remarks>
    private async Task<List<(StagedRow Row, RestoreClassification Classification, bool Overwrite)>> RestoreFilesForAsync(
        RestoreArchive archive,
        IEntityType entityType,
        List<(StagedRow Row, RestoreClassification Classification, bool Overwrite)> work,
        IReadOnlyDictionary<string, ArchiveFile> archivedFiles,
        List<(ArchiveFileSource Source, string StoredAs)> writtenFiles,
        Dictionary<string, int> totals,
        CancellationToken ct)
    {
        var entityName = entityType.ClrType.Name;

        var property = ArchiveFileSources.FileNamePropertyFor(entityName);
        if (property == null) return work;

        // Whether the row can exist without its file is a question the model already answers.
        // Contact.ProfileImageFileName is nullable — a contact without a photo is still a
        // contact. ProductImage.FileName is not, because a product image with no image is
        // nothing at all.
        var fileIsOptional = entityType.FindProperty(property)?.IsNullable ?? false;

        var rewritten = new List<(StagedRow, RestoreClassification, bool)>(work.Count);

        foreach (var item in work)
        {
            if (item.Classification != RestoreClassification.Restored)
            {
                rewritten.Add(item);
                continue;
            }

            var source = ArchiveFileSources.Extract(entityName, item.Row.Values);
            if (source == null)
            {
                rewritten.Add(item);
                continue;
            }

            var storedAs = await RestoreOneFileAsync(archive, source, archivedFiles, writtenFiles, totals, ct);

            if (storedAs == null)
            {
                // The file could not be put back. Where the column allows it, the row goes back
                // without one rather than claiming a file that is not there. Where it does not,
                // the row is dropped — inserting it would either fail on the not-null constraint
                // or leave a record whose only purpose is a file that does not exist.
                if (fileIsOptional)
                {
                    rewritten.Add((WithFileName(item.Row, property, null), item.Classification, item.Overwrite));
                }
                else
                {
                    totals["Skipped"] = totals.GetValueOrDefault("Skipped") + 1;
                }

                continue;
            }

            rewritten.Add((WithFileName(item.Row, property, storedAs), item.Classification, item.Overwrite));
        }

        return rewritten;
    }

    /// <summary>
    /// Copies one file out of the archive, checking it is what the manifest said it was.
    /// </summary>
    /// <returns>The name it was stored under, or null when it could not be restored.</returns>
    private async Task<string?> RestoreOneFileAsync(
        RestoreArchive archive, ArchiveFileSource source, IReadOnlyDictionary<string, ArchiveFile> archivedFiles,
        List<(ArchiveFileSource Source, string StoredAs)> writtenFiles,
        Dictionary<string, int> totals, CancellationToken ct)
    {
        try
        {
            var path = ArchiveFileSources.PathInArchive(source);
            archivedFiles.TryGetValue(path, out var described);

            // A handle of its own. Seeking the one the table enumeration is using would leave it
            // reading from somewhere it did not expect, and the rows after this file would be
            // decoded from the middle of something else.
            await using var stream = archive.OpenRead();
            using var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, leaveOpen: true);

            var entry = zip.GetEntry(path);
            if (entry == null)
            {
                totals["FilesMissing"] = totals.GetValueOrDefault("FilesMissing") + 1;
                return null;
            }

            // Buffered so the checksum can be checked before a byte is written. Storage takes a
            // stream it reads once, and a zip entry cannot be rewound.
            using var buffer = new MemoryStream();
            await using (var entryStream = entry.Open())
                await entryStream.CopyToAsync(buffer, ct);

            var actual = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(buffer.ToArray())).ToLowerInvariant();

            // No manifest entry is a refusal, not a pass. Treating a missing entry as "nothing to
            // check against" would let anyone bypass this by deleting the entry for the file they
            // substituted — the check would then only ever run on files nobody had tampered with.
            // A file the manifest does not describe has no business being restored.
            if (described?.Sha256 == null)
            {
                logger.LogWarning(
                    "Refused {Kind} for {OwnerId}: the manifest does not describe it",
                    source.Kind, source.OwnerId);
                totals["FilesRejected"] = totals.GetValueOrDefault("FilesRejected") + 1;
                return null;
            }

            if (!string.Equals(actual, described.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                // Refused rather than written. A file that does not match its manifest entry is
                // either damaged or substituted, and neither is something to put back silently.
                logger.LogWarning("Checksum mismatch restoring {Kind} for {OwnerId}", source.Kind, source.OwnerId);
                totals["FilesRejected"] = totals.GetValueOrDefault("FilesRejected") + 1;
                return null;
            }

            buffer.Position = 0;
            var storedAs = await ArchiveFileSources.SaveAsync(
                storage, source, buffer, described.ContentType ?? "application/octet-stream", ct);

            writtenFiles.Add((source, storedAs));
            totals["FilesRestored"] = totals.GetValueOrDefault("FilesRestored") + 1;
            return storedAs;
        }
        catch (Exception ex)
        {
            // One unreadable attachment must not cost the household the rest of the restore.
            logger.LogWarning(ex, "Could not restore {Kind} for {OwnerId}", source.Kind, source.OwnerId);
            totals["FilesFailed"] = totals.GetValueOrDefault("FilesFailed") + 1;
            return null;
        }
    }

    private static StagedRow WithFileName(StagedRow row, string property, string? storedAs)
    {
        var values = new Dictionary<string, JsonElement>(row.Values)
        {
            [property] = JsonSerializer.SerializeToElement(storedAs),
        };

        return row with { Values = values };
    }

    /// <summary>
    /// Removes files a failed restore had already written.
    /// </summary>
    /// <remarks>
    /// Best effort, and deliberately so. This runs while something has already gone wrong, and a
    /// storage error here would replace the real failure with a less useful one — the caller is
    /// about to report why the restore did not happen, and that is the more important message.
    /// A file left behind is waste; losing the reason is worse.
    /// </remarks>
    private async Task DeleteWrittenFilesAsync(
        IReadOnlyList<(ArchiveFileSource Source, string StoredAs)> written, CancellationToken ct)
    {
        foreach (var (source, storedAs) in written)
        {
            try
            {
                await ArchiveFileSources.DeleteAsync(storage, source, storedAs, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Could not remove {Kind} {FileName} after a failed restore", source.Kind, storedAs);
            }
        }
    }

    /// <summary>
    /// Whether this household has an export it could fall back on.
    /// </summary>
    private Task<bool> HasRecentBackupAsync(Guid tenantId, CancellationToken ct) =>
        context.HouseholdDataTransfers
            .AsNoTracking()
            .AnyAsync(t => t.TenantId == tenantId
                        && t.Kind == HouseholdDataTransferKind.Export
                        && t.Status == HouseholdDataTransferStatus.Completed
                        && t.ExpiresAt > DateTime.UtcNow, ct);

    private async Task<RestoreSummary> ToRestoreSummaryAsync(HouseholdDataTransfer t, CancellationToken ct)
    {
        var counts = ReadCounts(t);

        return new RestoreSummary
        {
            Id = t.Id,
            Status = t.Status.ToString(),
            // The name they chose, not the one it is stored under. Storage uses a name this code
            // picks, precisely so the uploaded one never reaches a path.
            FileName = t.OriginalUploadFileName ?? t.UploadFileName,
            ArchiveTakenAt = ReadArchiveDate(t),
            ProgressLabel = t.ProgressLabel,
            ProgressCurrent = t.ProgressCurrent,
            ProgressTotal = t.ProgressTotal,
            RestoredCount = counts.GetValueOrDefault(nameof(RestoreClassification.Restored)),
            UnchangedCount = counts.GetValueOrDefault(nameof(RestoreClassification.Unchanged)),
            ChangedSinceCount = counts.GetValueOrDefault(nameof(RestoreClassification.ChangedSince)),
            InvalidCount = counts.GetValueOrDefault(nameof(RestoreClassification.Invalid)),
            ChangedSincePolicy = t.ChangedSincePolicy.ToString(),
            AppliedInserted = counts.GetValueOrDefault("Inserted"),
            AppliedUpdated = counts.GetValueOrDefault("Updated"),
            AppliedSkipped = counts.GetValueOrDefault("Skipped"),
            AppliedFailed = counts.GetValueOrDefault("Failed"),
            HasRecentBackup = await HasRecentBackupAsync(t.TenantId, ct),
            ErrorCode = t.ErrorCode,
            ErrorMessage = t.ErrorMessage,
        };
    }

    private static DateTime? ReadArchiveDate(HouseholdDataTransfer t)
    {
        if (t.ManifestJson == null) return null;

        var manifest = JsonSerializer.Deserialize<ArchiveManifest>(
            t.ManifestJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return manifest?.GeneratedAt;
    }

    /// <summary>
    /// What an uploaded archive is stored as. Fixed, because the name it arrived with is
    /// attacker-controlled and both backends build a path from it. One per transfer id, so a
    /// constant cannot collide.
    /// </summary>
    private const string StoredUploadName = "archive.zip";

    /// <summary>
    /// The uploaded name reduced to something safe to show a person.
    /// </summary>
    /// <remarks>
    /// Kept only so the UI can say which file was chosen. It never reaches a filesystem, a URL or
    /// a query; anything structural is dropped and the rest is bounded.
    /// </remarks>
    private static string DisplayNameFor(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "archive.zip";

        var name = fileName;

        // Both separators, whatever the client's platform.
        var lastSlash = name.LastIndexOfAny(['/', '\\']);
        if (lastSlash >= 0) name = name[(lastSlash + 1)..];

        name = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();

        if (name.Length == 0 || name.All(c => c == '.')) return "archive.zip";
        return name.Length > 120 ? name[..120] : name;
    }

    private static Dictionary<string, int> ReadCounts(HouseholdDataTransfer transfer) =>
        transfer.RestoreCountsJson == null
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, int>>(transfer.RestoreCountsJson) ?? [];

}

/// <summary>
/// An archive this household cannot restore, with a reason worth showing the user.
/// </summary>
public sealed class RestoreRefusedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
