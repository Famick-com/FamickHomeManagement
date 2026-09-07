using System.Data;
using Npgsql;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.DataPortability;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <inheritdoc />
public sealed class HouseholdDataPortabilityService(
    HomeManagementDbContext context,
    ITenantProvider tenantProvider,
    IFileStorageService storage,
    IFileAccessTokenService tokenService,
    HouseholdArchiveWriter writer,
    ILogger<HouseholdDataPortabilityService> logger,
    IMessageService? messages = null,
    IDistributedLockService? locks = null,
    IMultiTenancyOptions? multiTenancy = null)
    : IHouseholdDataPortabilityService
{
    /// <summary>How long a finished archive stays downloadable.</summary>
    private static readonly TimeSpan ArchiveLifetime = TimeSpan.FromDays(7);

    /// <summary>A Running row whose heartbeat is older than this belongs to a dead worker.</summary>
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(5);

    private const long MaxUploadBytes = 2L * 1024 * 1024 * 1024;

    public async Task<DataPortabilityCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await ReconcileAbandonedRunsAsync(tenantId, ct);

        // AsNoTracking matters here, not just for speed. Reconciliation above updates rows
        // directly in the database, which leaves any already-tracked copy in this context stale —
        // and a tracked copy wins over the database's answer on identity resolution. A read that
        // only feeds a DTO should not be holding opinions about entity state anyway.
        var recent = await context.HouseholdDataTransfers
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Kind == HouseholdDataTransferKind.Export)
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        var active = recent.FirstOrDefault(t =>
            t.Status is HouseholdDataTransferStatus.Queued or HouseholdDataTransferStatus.Running);

        var latest = recent.FirstOrDefault(t =>
            t.Status == HouseholdDataTransferStatus.Completed &&
            t.ExpiresAt > DateTime.UtcNow);

        return new DataPortabilityCapabilities
        {
            ExportSupported = true,
            RestoreSupported = false,
            MaxUploadBytes = MaxUploadBytes,
            ActiveExportId = active?.Id,
            LatestExport = latest == null ? null : ToSummary(latest),
        };
    }

    public async Task<DataExportSummary> StartExportAsync(
        StartExportRequest request, Guid requestedByUserId, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        await ReconcileAbandonedRunsAsync(tenantId, ct);

        // Hand back the run already in flight rather than starting a second. Two concurrent
        // exports of one household build two large archives to no purpose.
        var inFlight = await context.HouseholdDataTransfers
            .Where(t => t.TenantId == tenantId
                     && t.Kind == HouseholdDataTransferKind.Export
                     && (t.Status == HouseholdDataTransferStatus.Queued
                      || t.Status == HouseholdDataTransferStatus.Running))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (inFlight != null) return ToSummary(inFlight);

        var transfer = new HouseholdDataTransfer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kind = HouseholdDataTransferKind.Export,
            Status = HouseholdDataTransferStatus.Queued,
            RequestedByUserId = requestedByUserId,
            IncludeFiles = request.IncludeFiles,
        };

        context.HouseholdDataTransfers.Add(transfer);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            // Lost the race to another request that queued one between our check and our insert.
            // The database is the thing that actually decides; hand back whatever won.
            context.Entry(transfer).State = EntityState.Detached;

            var winner = await context.HouseholdDataTransfers
                .Where(t => t.TenantId == tenantId
                         && t.Kind == HouseholdDataTransferKind.Export
                         && (t.Status == HouseholdDataTransferStatus.Queued
                          || t.Status == HouseholdDataTransferStatus.Running))
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (winner != null) return ToSummary(winner);
            throw;
        }

        logger.LogInformation("Queued export {TransferId} for tenant {TenantId}", transfer.Id, tenantId);
        return ToSummary(transfer);
    }

    public async Task<DataExportSummary?> GetExportAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        return transfer == null ? null : ToSummary(transfer);
    }

    public async Task<ArchiveManifest?> GetManifestAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        if (transfer?.ManifestJson == null) return null;

        return JsonSerializer.Deserialize<ArchiveManifest>(transfer.ManifestJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    public async Task<ExportDownloadResult> OpenArchiveAsync(
        Guid transferId, long? rangeStart, long? rangeEnd, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);

        if (transfer?.ArchiveFileName == null) return ExportDownloadResult.Unavailable;
        if (transfer.Status != HouseholdDataTransferStatus.Completed) return ExportDownloadResult.Unavailable;

        // Expiry is answered from the record, not from whether the object happens to still be
        // there. Otherwise the answer depends on which side deleted first.
        if (transfer.ExpiresAt is null || transfer.ExpiresAt <= DateTime.UtcNow) return ExportDownloadResult.Unavailable;

        var info = await storage.GetExportArchiveInfoAsync(transferId, transfer.ArchiveFileName, ct);
        if (info == null) return ExportDownloadResult.Unavailable;

        var range = ResolveRange(rangeStart, rangeEnd, info.Length);
        if (range is null) return ExportDownloadResult.RangeNotSatisfiable;

        var (start, end) = range.Value;

        var stream = await storage.GetExportArchiveStreamAsync(
            transferId, transfer.ArchiveFileName, start, end, ct);

        return stream == null
            ? ExportDownloadResult.Unavailable
            : ExportDownloadResult.Ok(new ExportDownload(stream, transfer.ArchiveFileName, info.Length, start, end));
    }

    /// <summary>
    /// Turns whatever the client asked for into concrete bounds, or null when it cannot be served.
    /// </summary>
    /// <remarks>
    /// Three forms, and only the first is obvious. <c>bytes=100-200</c> is explicit;
    /// <c>bytes=100-</c> runs to the end; and <c>bytes=-500</c> is a suffix asking for the last
    /// 500 bytes, which arrives with no start at all. Left unresolved, that last form reads as
    /// "no range" and quietly returns the whole archive with a 200.
    /// </remarks>
    private static (long Start, long End)? ResolveRange(long? rangeStart, long? rangeEnd, long totalLength)
    {
        if (rangeStart is null && rangeEnd is null) return (0, totalLength - 1);
        if (totalLength == 0) return null;

        if (rangeStart is null)
        {
            // Suffix: the last N bytes, clamped to the whole archive when N exceeds it.
            var suffixLength = rangeEnd!.Value;
            if (suffixLength <= 0) return null;

            var suffixStart = Math.Max(0, totalLength - suffixLength);
            return (suffixStart, totalLength - 1);
        }

        // A start at or past the end is unsatisfiable rather than empty — answering it with zero
        // bytes and a 206 tells the client the file ended where it did not.
        if (rangeStart.Value < 0 || rangeStart.Value >= totalLength) return null;

        var resolvedEnd = Math.Min(rangeEnd ?? totalLength - 1, totalLength - 1);
        return resolvedEnd < rangeStart.Value ? null : (rangeStart.Value, resolvedEnd);
    }

    public async Task<string?> GetDownloadLinkAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);

        if (transfer?.ArchiveFileName == null) return null;
        if (transfer.Status != HouseholdDataTransferStatus.Completed) return null;
        if (transfer.ExpiresAt is null || transfer.ExpiresAt <= DateTime.UtcNow) return null;

        // Minutes, not days. This one only has to survive the gap between the click and the
        // download starting, so a leaked URL from a browser history is worth little.
        var token = tokenService.GenerateToken(
            "export-archive", transfer.Id, transfer.TenantId, expirationMinutes: 15);

        return storage.GetExportArchiveUrl(transfer.Id, token);
    }

    public async Task<bool> DeleteExportAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await FindAsync(transferId, ct);
        if (transfer == null) return false;

        // Refused while a worker still has it. Marking a running export Expired does not stop the
        // worker: it goes on to save the archive, write Completed over the Expired, and email a
        // download link for something the user just deleted. Cancelling properly would need the
        // worker to check state before publishing and again before notifying, which is worth
        // doing when there is a cancel button to justify it.
        if (transfer.Status is HouseholdDataTransferStatus.Queued
                             or HouseholdDataTransferStatus.Running
                             or HouseholdDataTransferStatus.Applying)
        {
            return false;
        }

        if (transfer.ArchiveFileName != null)
            await storage.DeleteExportArchiveAsync(transferId, transfer.ArchiveFileName, ct);

        transfer.Status = HouseholdDataTransferStatus.Expired;
        transfer.ArchiveFileName = null;
        await context.SaveChangesAsync(ct);

        return true;
    }

    public async Task<bool> RunExportAsync(Guid transferId, CancellationToken ct = default)
    {
        var transfer = await context.HouseholdDataTransfers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == transferId, ct);

        if (transfer == null || transfer.Status != HouseholdDataTransferStatus.Queued) return false;

        var tenantId = transfer.TenantId;

        // The worker has no HttpContext, so the tenant has to be set explicitly. Without this the
        // global query filter treats the context as "no tenant" and matches every household —
        // which on a bulk read means archiving the whole platform.
        tenantProvider.SetTenantId(tenantId);

        await using var lease = locks == null
            ? null
            : await locks.TryAcquireLockAsync($"household-export:{tenantId}", TimeSpan.FromMinutes(30), ct);

        if (locks != null && lease == null)
        {
            logger.LogInformation("Export {TransferId} skipped: another worker holds the lock", transferId);
            return false;
        }

        transfer.Status = HouseholdDataTransferStatus.Running;
        transfer.StartedAt = DateTime.UtcNow;
        transfer.HeartbeatAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        var tempPath = Path.Combine(Path.GetTempPath(), $"famick-export-{transferId:N}.zip");

        try
        {
            var manifest = await BuildArchiveAsync(transfer, tenantId, tempPath, ct);

            var fileName = BuildFileName(manifest.Household.Name);

            // Size read before the upload, not after. The interface says the caller owns the
            // stream, but reading a property off it once it has been handed away makes this code
            // depend on every implementation honouring that — and one did not.
            var archiveBytes = new FileInfo(tempPath).Length;

            await using (var completed = File.OpenRead(tempPath))
            {
                await storage.SaveExportArchiveAsync(transferId, completed, fileName, ct);
            }

            transfer.ArchiveFileName = fileName;
            transfer.ArchiveBytes = archiveBytes;

            transfer.ManifestJson = JsonSerializer.Serialize(manifest);
            transfer.Status = HouseholdDataTransferStatus.Completed;
            transfer.CompletedAt = DateTime.UtcNow;
            transfer.ExpiresAt = DateTime.UtcNow.Add(ArchiveLifetime);
            transfer.ProgressCurrent = transfer.ProgressTotal;
            transfer.ProgressLabel = null;

            await context.SaveChangesAsync(ct);
            await NotifyAsync(transfer, manifest, ct);

            logger.LogInformation("Export {TransferId} completed: {Rows} rows, {Files} files",
                transferId, manifest.Counts.Rows, manifest.Counts.Files);
        }
        catch (OperationCanceledException)
        {
            transfer.Status = HouseholdDataTransferStatus.Cancelled;
            await context.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Export {TransferId} failed", transferId);

            transfer.Status = HouseholdDataTransferStatus.Failed;
            transfer.ErrorCode = "EXPORT_FAILED";
            transfer.ErrorMessage = ex.Message;
            transfer.CompletedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(CancellationToken.None);
        }
        finally
        {
            // A crashed export otherwise leaves a multi-gigabyte temp file behind, and they add up.
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch (Exception ex) { logger.LogWarning(ex, "Could not remove temp archive {Path}", tempPath); }
            }
        }

        return true;
    }

    private async Task<ArchiveManifest> BuildArchiveAsync(
        HouseholdDataTransfer transfer, Guid tenantId, string tempPath, CancellationToken ct)
    {
        var tenant = await context.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        // A connection of our own, not the one EF is using.
        //
        // The snapshot has to stay open for the whole read, which is minutes. On EF's connection
        // that breaks progress twice over. Every progress write would enlist in the snapshot and
        // stay invisible to the requests polling for it until the export finished — which is the
        // entire thing progress-in-Postgres exists to avoid. And because the application
        // configures Npgsql with EnableRetryOnFailure, SaveChanges inside a user-initiated
        // transaction throws outright: a retrying strategy cannot know whether replaying a
        // half-finished transaction is safe, so it refuses rather than guess.
        //
        // Wrapping the unit in CreateExecutionStrategy().ExecuteAsync is the usual answer to that
        // second problem, and it is the wrong shape here — a retry would restart a multi-gigabyte
        // archive from the beginning, over a partly written file. Separating the connections fixes
        // both, and costs nothing: the reads never needed to share EF's.
        var connectionString = context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("No connection string is configured for the database.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        // One consistent picture of the household. A five-minute export must not catch a product
        // whose images were deleted halfway through. Read-only is declared rather than merely
        // intended: nothing about building an archive should be able to write.
        await using var snapshot = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        await using (var readOnly = connection.CreateCommand())
        {
            readOnly.Transaction = snapshot;
            readOnly.CommandText = "SET TRANSACTION READ ONLY";
            await readOnly.ExecuteNonQueryAsync(ct);
        }

        await using var file = File.Create(tempPath);

        var manifest = await writer.WriteAsync(
            connection,
            snapshot,
            context.Model,
            tenantId,
            new ArchiveHousehold { Id = tenantId, Name = tenant?.Name ?? "Household" },
            new ArchiveSource
            {
                Mode = multiTenancy?.IsMultiTenantEnabled == true ? "cloud" : "selfHosted",
                TenantId = tenantId,
            },
            appVersion: typeof(HouseholdDataPortabilityService).Assembly.GetName().Version?.ToString(),
            file,
            transfer.IncludeFiles,
            progress: (label, current, total, token) => ReportProgressAsync(transfer, label, current, total, token),
            ct);

        await snapshot.CommitAsync(ct);
        return manifest;
    }

    /// <summary>
    /// Writes progress where the polling request can see it.
    /// </summary>
    /// <remarks>
    /// Deliberately a database write rather than a field. The cloud runs more than one instance,
    /// so a field is invisible to whichever instance answers the poll.
    /// </remarks>
    private async Task ReportProgressAsync(
        HouseholdDataTransfer transfer, string label, long current, long total, CancellationToken ct)
    {
        transfer.ProgressLabel = label;
        transfer.ProgressCurrent = current;
        transfer.ProgressTotal = total;
        transfer.HeartbeatAt = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);
    }

    private async Task NotifyAsync(HouseholdDataTransfer transfer, ArchiveManifest manifest, CancellationToken ct)
    {
        if (messages == null) return;

        var user = await context.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == transfer.RequestedByUserId, ct);

        if (user == null || string.IsNullOrWhiteSpace(user.Email)) return;

        // A long-lived token so the link survives until the archive itself expires; the archive,
        // not the token, is what limits the window.
        var token = tokenService.GenerateToken(
            "export-archive", transfer.Id, transfer.TenantId,
            expirationMinutes: (int)ArchiveLifetime.TotalMinutes);

        var downloadLink = storage.GetExportArchiveUrl(transfer.Id, token);

        // Every other URL this storage service builds is handed to a browser that already knows
        // what host it is on, so a relative one works there and nobody notices the base URL is
        // unset. This is the only one that leaves the building, and an email client has nothing
        // to resolve it against — the recipient gets a link that cannot work.
        //
        // Refusing to send beats sending a dead link: the archive is finished and downloadable in
        // the app either way, and a broken email is worse than none because it looks like the
        // feature failed.
        // UriKind.Absolute alone is not the test. On Unix a leading slash parses happily as an
        // absolute file:// URI, so "/api/v1/..." would pass and the check would do nothing on the
        // very platform this runs on. It has to be a web address: http or https, with a host.
        if (!Uri.TryCreate(downloadLink, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(parsed.Host))
        {
            logger.LogError(
                "Export {TransferId} finished but no notification was sent: the download link " +
                "'{Link}' is not absolute. Set the BaseUrl configuration key (for example " +
                "https://app.famick.com) so emailed links have a host.",
                transfer.Id, downloadLink);
            return;
        }

        try
        {
            await messages.SendTransactionalAsync(user.Email, MessageType.DataExportReady, new DataExportReadyData
            {
                UserName = user.FirstName,
                HouseholdName = manifest.Household.Name,
                DownloadLink = downloadLink,
                ExpiresOn = transfer.ExpiresAt?.ToString("d MMMM yyyy") ?? string.Empty,
                SizeDescription = DescribeSize(transfer.ArchiveBytes ?? 0),
                RowCount = manifest.Counts.Rows,
                FileCount = manifest.Counts.Files,
                MissingFileCount = manifest.Counts.MissingFiles,
            }, ct);
        }
        catch (Exception ex)
        {
            // The archive is built and downloadable in the app; a failed email must not undo that.
            logger.LogWarning(ex, "Export {TransferId} finished but the notification email failed", transfer.Id);
        }
    }

    /// <summary>
    /// Marks runs whose worker died as failed, so they stop blocking the household.
    /// </summary>
    private async Task ReconcileAbandonedRunsAsync(Guid tenantId, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow - HeartbeatTimeout;

        // A single conditional update rather than read-then-write. The condition is re-evaluated
        // by the database at write time, so a worker that sent a heartbeat between the read and
        // the write keeps its run instead of having it declared lost underneath it.
        var reconciled = await context.HouseholdDataTransfers
            .Where(t => t.TenantId == tenantId
                     && t.Status == HouseholdDataTransferStatus.Running
                     && t.HeartbeatAt < cutoff)
            .ExecuteUpdateAsync(set => set
                .SetProperty(t => t.Status, HouseholdDataTransferStatus.Failed)
                .SetProperty(t => t.ErrorCode, "WORKER_LOST")
                .SetProperty(t => t.ErrorMessage, "The export stopped unexpectedly. Please try again.")
                .SetProperty(t => t.CompletedAt, DateTime.UtcNow), ct);

        if (reconciled > 0)
            logger.LogWarning("Reconciled {Count} abandoned transfers for tenant {TenantId}", reconciled, tenantId);
    }

    private Task<HouseholdDataTransfer?> FindAsync(Guid transferId, CancellationToken ct)
    {
        var tenantId = RequireTenant();

        // Scoped explicitly as well as by the query filter: this is the lookup behind a download,
        // and one household must never reach another's archive.
        return context.HouseholdDataTransfers
            .FirstOrDefaultAsync(t => t.Id == transferId && t.TenantId == tenantId, ct);
    }

    private Guid RequireTenant() =>
        tenantProvider.TenantId
        ?? throw new InvalidOperationException(
            "No tenant in context. A household-wide read without a tenant matches every " +
            "household, so this refuses rather than guessing.");

    private static DataExportSummary ToSummary(HouseholdDataTransfer t)
    {
        var manifest = t.ManifestJson == null
            ? null
            : JsonSerializer.Deserialize<ArchiveManifest>(t.ManifestJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return new DataExportSummary
        {
            Id = t.Id,
            Status = t.Status.ToString(),
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt,
            ExpiresAt = t.ExpiresAt,
            ProgressLabel = t.ProgressLabel,
            ProgressCurrent = t.ProgressCurrent,
            ProgressTotal = t.ProgressTotal,
            FileName = t.ArchiveFileName,
            Bytes = t.ArchiveBytes,
            RowCount = manifest?.Counts.Rows ?? 0,
            FileCount = manifest?.Counts.Files ?? 0,
            MissingFileCount = manifest?.Counts.MissingFiles ?? 0,
            ErrorCode = t.ErrorCode,
            ErrorMessage = t.ErrorMessage,
        };
    }

    private static string BuildFileName(string householdName)
    {
        var slug = new string(householdName.ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');

        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        if (slug.Length > 40) slug = slug[..40].Trim('-');
        if (slug.Length == 0) slug = "household";

        return $"famick-export-{slug}-{DateTime.UtcNow:yyyyMMdd-HHmm}.zip";
    }

    private static string DescribeSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} bytes",
        < 1024 * 1024 => $"{bytes / 1024.0:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB",
    };
}
