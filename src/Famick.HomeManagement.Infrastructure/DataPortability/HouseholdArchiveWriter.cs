using System.Data.Common;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.DataPortability;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// Builds one household's archive.
/// </summary>
/// <remarks>
/// <para>
/// Everything streams. A household's data does not fit comfortably in memory once photos and
/// documents are in it, so rows go out a line at a time and files are copied straight through.
/// Nothing is materialised into a list, and the archive is written to a stream the caller owns
/// rather than a buffer.
/// </para>
/// <para>
/// Rows are read inside a repeatable-read snapshot so a long export sees one consistent picture
/// of the household. Files are copied <em>after</em> that snapshot is released: holding a
/// transaction open across a file copy would pin the database for the length of an upload, and
/// the cost of letting go is only that a file deleted in between is reported as missing, which is
/// exactly what the missing-file list is for.
/// </para>
/// </remarks>
public sealed class HouseholdArchiveWriter(
    IFileStorageService storage,
    ILogger<HouseholdArchiveWriter> logger)
{
    private static readonly JsonSerializerOptions RowJson = new()
    {
        // Keys are CLR property names, so a column rename in a migration does not strand old
        // archives. The reader matches on these.
        WriteIndented = false,
    };

    /// <summary>
    /// Writes the archive for <paramref name="tenantId"/> into <paramref name="destination"/>.
    /// </summary>
    /// <param name="connection">An open connection, already inside a read-only snapshot.</param>
    /// <param name="progress">Called as tables complete, for the UI to poll.</param>
    public async Task<ArchiveManifest> WriteAsync(
        DbConnection connection,
        IModel model,
        Guid tenantId,
        ArchiveHousehold household,
        ArchiveSource source,
        string? appVersion,
        Stream destination,
        bool includeFiles,
        Func<string, long, long, CancellationToken, Task>? progress,
        CancellationToken ct)
    {
        var plan = ExportPlan.Build(model);

        var manifest = new ArchiveManifest
        {
            GeneratedAt = DateTime.UtcNow,
            Generator = new ArchiveGenerator { AppVersion = appVersion },
            Source = source,
            Household = household,
            ExcludedEntities = ExportPlan.Exclusions(model).ToList(),
        };

        var collectedFiles = new List<ArchiveFileSource>();

        using var archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

        var tableIndex = 0;
        foreach (var table in plan)
        {
            ct.ThrowIfCancellationRequested();
            tableIndex++;

            if (progress != null)
                await progress(table.EntityName, tableIndex, plan.Count, ct);

            var entry = await WriteTableAsync(archive, connection, table, tenantId, collectedFiles, ct);
            manifest.Tables.Add(entry);
            manifest.Counts.Rows += entry.RowCount;
        }

        if (includeFiles)
            await WriteFilesAsync(archive, collectedFiles, manifest, ct);
        else
            logger.LogInformation("Export for tenant {TenantId} omits files by request", tenantId);

        manifest.Counts.Files = manifest.Files.Count;
        manifest.Counts.MissingFiles = manifest.MissingFiles.Count;

        await WriteEntryAsync(archive, "manifest.json",
            JsonSerializer.SerializeToUtf8Bytes(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);

        return manifest;
    }

    private async Task<ArchiveTable> WriteTableAsync(
        ZipArchive archive,
        DbConnection connection,
        ExportTable table,
        Guid tenantId,
        List<ArchiveFileSource> collectedFiles,
        CancellationToken ct)
    {
        var entry = archive.CreateEntry(table.FileName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var rowCount = 0L;
        var watchForFiles = ArchiveFileSources.CarriesFiles(table.EntityName);

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = table.Sql.Replace("{0}", "@tenantId");

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@tenantId";
            parameter.Value = tenantId;
            command.Parameters.Add(parameter);

            // SequentialAccess: the reader does not buffer a whole row, which matters for the
            // tables carrying long text.
            await using var reader = await command.ExecuteReaderAsync(
                System.Data.CommandBehavior.SequentialAccess, ct);

            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(table.Columns.Count);

                for (var i = 0; i < table.Columns.Count; i++)
                    row[table.Columns[i].Property] = await reader.IsDBNullAsync(i, ct)
                        ? null
                        : reader.GetValue(i);

                if (watchForFiles)
                {
                    var file = ArchiveFileSources.Extract(table.EntityName, row);
                    if (file != null) collectedFiles.Add(file);
                }

                var line = JsonSerializer.SerializeToUtf8Bytes(row, RowJson);
                hash.AppendData(line);
                hash.AppendData("\n"u8.ToArray());

                await entryStream.WriteAsync(line, ct);
                await entryStream.WriteAsync("\n"u8.ToArray(), ct);

                rowCount++;
            }
        }

        return new ArchiveTable
        {
            Order = table.Order,
            Entity = table.EntityName,
            Table = table.TableName,
            File = table.FileName,
            RowCount = rowCount,
            Sha256 = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
            Columns = table.Columns.Select(c => c.Property).ToList(),
            ExcludedColumns = table.ExcludedColumns.ToList(),
            TenantScope = table.TenantScope,
            TenantPath = table.TenantPath.ToList(),
        };
    }

    private async Task WriteFilesAsync(
        ZipArchive archive,
        IReadOnlyList<ArchiveFileSource> files,
        ArchiveManifest manifest,
        CancellationToken ct)
    {
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();

            var unreadable = ArchiveFileSources.UnreadableReason(file.OwnerEntity);
            if (unreadable != null)
            {
                manifest.MissingFiles.Add(new ArchiveMissingFile(file.Kind, file.OwnerId, file.FileName, unreadable));
                continue;
            }

            Stream? source;
            try
            {
                source = await ArchiveFileSources.OpenAsync(storage, file, ct);
            }
            catch (Exception ex)
            {
                // One unreadable file must not cost the household its whole archive.
                logger.LogWarning(ex, "Could not read {Kind} {FileName} for {OwnerId}",
                    file.Kind, file.FileName, file.OwnerId);
                manifest.MissingFiles.Add(new ArchiveMissingFile(
                    file.Kind, file.OwnerId, file.FileName, "Storage returned an error reading this file."));
                continue;
            }

            if (source == null)
            {
                manifest.MissingFiles.Add(new ArchiveMissingFile(
                    file.Kind, file.OwnerId, file.FileName,
                    "Referenced by the database but not present in storage."));
                continue;
            }

            var path = ArchiveFileSources.PathInArchive(file);
            var entry = archive.CreateEntry(path, CompressionLevel.Fastest);

            long bytes;
            string checksum;

            await using (source)
            await using (var entryStream = entry.Open())
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[81920];
                bytes = 0;
                int read;

                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await entryStream.WriteAsync(buffer.AsMemory(0, read), ct);
                    bytes += read;
                }

                checksum = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }

            manifest.Files.Add(new ArchiveFile
            {
                Kind = file.Kind,
                OwnerEntity = file.OwnerEntity,
                OwnerId = file.OwnerId,
                Path = path,
                Sha256 = checksum,
                Bytes = bytes,
            });

            manifest.Counts.Bytes += bytes;
        }
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string path, byte[] content, CancellationToken ct)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await stream.WriteAsync(content, ct);
    }
}
