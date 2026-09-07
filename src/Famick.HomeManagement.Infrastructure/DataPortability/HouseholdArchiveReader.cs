using System.IO.Compression;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.DataPortability;

namespace Famick.HomeManagement.Infrastructure.DataPortability;

/// <summary>
/// One row read back out of an archive.
/// </summary>
/// <param name="Entity">The CLR entity name, matched against the model by name.</param>
/// <param name="Id">The row's primary key, preserved from the export.</param>
/// <param name="Values">Property name to raw JSON value, exactly as written.</param>
public sealed record StagedRow(string Entity, Guid Id, IReadOnlyDictionary<string, JsonElement> Values);

/// <summary>
/// Why an archive cannot be restored into this household.
/// </summary>
public enum ArchiveRejection
{
    None,

    /// <summary>Not a readable archive, or the manifest is missing.</summary>
    Unreadable,

    /// <summary>Written by a newer version whose envelope this reader does not understand.</summary>
    TooNew,

    /// <summary>
    /// Belongs to a different household. Restore puts a household's own archive back; moving data
    /// between households or deployments is a different operation.
    /// </summary>
    DifferentHousehold,

    /// <summary>
    /// Expands to more than this reader will read. Either an implausible household or a file
    /// built to exhaust the server.
    /// </summary>
    TooLarge,
}

public sealed record ArchiveOpenResult(ArchiveRejection Rejection, ArchiveManifest? Manifest = null)
{
    public bool IsUsable => Rejection == ArchiveRejection.None && Manifest != null;
}

/// <summary>
/// Reads an archive back.
/// </summary>
/// <remarks>
/// The reader does not hold the archive open across the whole restore. It is opened to inspect,
/// again to classify, and again to apply — a restore is not a hot path, and re-reading is cheaper
/// than keeping a multi-gigabyte stream and its zip state alive across a user's decision.
/// </remarks>
public sealed class HouseholdArchiveReader
{
    /// <summary>The newest envelope this reader understands.</summary>
    public const int SupportedSchemaVersion = 1;

    /// <summary>
    /// The most an archive may expand to. Well beyond any real household, and far short of what a
    /// deliberately crafted one can reach.
    /// </summary>
    /// <remarks>
    /// An upload is capped at two gigabytes, but zip is a compressing format and text compresses
    /// enormously — a few megabytes of zeroes expands to terabytes. Without a ceiling the reader
    /// would happily follow it until the process died, from a file any admin is allowed to send.
    /// </remarks>
    private const long MaxUncompressedBytes = 8L * 1024 * 1024 * 1024;

    /// <summary>The most rows one table may contain, for the same reason.</summary>
    public const int MaxRowsPerTable = 2_000_000;

    private static readonly JsonSerializerOptions ManifestJson = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Reads the manifest and decides whether this archive may be restored here.
    /// </summary>
    /// <remarks>
    /// The household check is the precondition the whole design rests on. Because it holds, every
    /// Id in the archive was this household's when it was written, so identity matching is exact
    /// and there is no cross-household collision to reason about.
    /// </remarks>
    public ArchiveOpenResult Open(Stream archive, Guid targetTenantId)
    {
        ArchiveManifest? manifest;

        try
        {
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);

            var entry = zip.GetEntry("manifest.json");
            if (entry == null) return new ArchiveOpenResult(ArchiveRejection.Unreadable);

            using var stream = entry.Open();
            manifest = JsonSerializer.Deserialize<ArchiveManifest>(stream, ManifestJson);

            // Declared sizes, checked before a single entry is read. A zip's directory states
            // what each entry expands to, so a bomb can be turned away on its own paperwork
            // rather than by watching the process run out of memory.
            var declared = zip.Entries.Sum(e => e.Length);
            if (declared > MaxUncompressedBytes)
                return new ArchiveOpenResult(ArchiveRejection.TooLarge);
        }
        catch (InvalidDataException)
        {
            return new ArchiveOpenResult(ArchiveRejection.Unreadable);
        }
        catch (JsonException)
        {
            return new ArchiveOpenResult(ArchiveRejection.Unreadable);
        }

        if (manifest == null) return new ArchiveOpenResult(ArchiveRejection.Unreadable);

        // Refused rather than attempted. A newer envelope may have moved things this reader would
        // silently not find, and a half-restored household is worse than a refused one.
        if (manifest.MinimumReaderVersion > SupportedSchemaVersion)
            return new ArchiveOpenResult(ArchiveRejection.TooNew, manifest);

        if (manifest.Source.TenantId != targetTenantId)
            return new ArchiveOpenResult(ArchiveRejection.DifferentHousehold, manifest);

        return new ArchiveOpenResult(ArchiveRejection.None, manifest);
    }

    /// <summary>
    /// Streams the rows of one table out of the archive.
    /// </summary>
    /// <remarks>
    /// Yields as it reads rather than returning a list: a table can hold hundreds of thousands of
    /// rows and the caller only ever needs one at a time.
    /// </remarks>
    public async IAsyncEnumerable<StagedRow> ReadTableAsync(
        Stream archive,
        ArchiveTable table,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);

        var entry = zip.GetEntry(table.File);
        if (entry == null) yield break;

        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);

        var rows = 0;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            ct.ThrowIfCancellationRequested();
            if (line.Length == 0) continue;

            // Stops here rather than letting a crafted file decide how long this runs. The caller
            // collects what it is given, so an unbounded table would be an unbounded list.
            if (++rows > MaxRowsPerTable)
                throw new InvalidDataException(
                    $"'{table.Entity}' contains more than {MaxRowsPerTable:N0} rows, which is beyond what a restore will read.");

            Dictionary<string, JsonElement>? values;
            try
            {
                values = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(line);
            }
            catch (JsonException)
            {
                // One malformed line must not cost the household the rest of the table. The row is
                // dropped here and shows up as a shortfall against the manifest's row count.
                continue;
            }

            if (values == null) continue;
            if (!values.TryGetValue("Id", out var idValue)) continue;
            if (!idValue.TryGetGuid(out var id)) continue;

            yield return new StagedRow(table.Entity, id, values);
        }
    }
}
