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

    private readonly ArchiveReaderLimits _limits;

    public HouseholdArchiveReader() : this(ArchiveReaderLimits.Default) { }

    /// <summary>
    /// Limits are injectable so a test can use small ones. Generating an archive that trips the
    /// production ceiling means deflating gigabytes on every run, which buys nothing a
    /// proportionally smaller archive and a smaller ceiling do not.
    /// </summary>
    public HouseholdArchiveReader(ArchiveReaderLimits limits) => _limits = limits;

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
            if (declared > _limits.MaxUncompressedBytes)
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

        // Source has an initializer, but a manifest saying "source": null replaces it — the
        // initializer only covers the property being absent. Without this the reader throws
        // where it meant to refuse, and the refusal is reported as a failure instead.
        if (manifest?.Source == null) return new ArchiveOpenResult(ArchiveRejection.Unreadable);

        // Refused rather than attempted. A newer envelope may have moved things this reader would
        // silently not find, and a half-restored household is worse than a refused one.
        if (manifest.MinimumReaderVersion > SupportedSchemaVersion)
            return new ArchiveOpenResult(ArchiveRejection.TooNew, manifest);

        if (manifest.Source.TenantId != targetTenantId)
            return new ArchiveOpenResult(ArchiveRejection.DifferentHousehold, manifest);

        return new ArchiveOpenResult(ArchiveRejection.None, manifest);
    }

    /// <summary>
    /// Reads one line, refusing to grow past <paramref name="maxBytes"/>.
    /// </summary>
    private static async Task<string?> ReadBoundedLineAsync(
        StreamReader reader, long maxBytes, string entity, CancellationToken ct)
    {
        var builder = new System.Text.StringBuilder();
        var buffer = new char[1];

        while (await reader.ReadAsync(buffer, ct) == 1)
        {
            if (buffer[0] == '\n') return builder.ToString();
            if (buffer[0] == '\r') continue;

            builder.Append(buffer[0]);

            if (builder.Length > maxBytes)
                throw new InvalidDataException(
                    $"A row in '{entity}' is longer than {maxBytes:N0} characters, which is beyond what a restore will read.");
        }

        return builder.Length > 0 ? builder.ToString() : null;
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

        // Read with a length cap rather than ReadLineAsync, which materialises whatever it finds
        // before anything gets to object. An archive well inside the expansion ceiling can still
        // hold one line long enough to exhaust the worker, and a row that large is not a row.
        while (await ReadBoundedLineAsync(reader, _limits.MaxRowBytes, table.Entity, ct) is { } line)
        {
            ct.ThrowIfCancellationRequested();
            if (line.Length == 0) continue;

            // Stops here rather than letting a crafted file decide how long this runs. The caller
            // collects what it is given, so an unbounded table would be an unbounded list.
            if (++rows > _limits.MaxRowsPerTable)
                throw new InvalidDataException(
                    $"'{table.Entity}' contains more than {_limits.MaxRowsPerTable:N0} rows, which is beyond what a restore will read.");

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

/// <summary>
/// What a restore will and will not read.
/// </summary>
/// <remarks>
/// An archive arrives from outside, so every one of these is the difference between a refusal and
/// a worker running out of memory. Injectable so tests can use small numbers — proving the
/// ceiling works does not require building something the size of the real one.
/// </remarks>
/// <param name="MaxUncompressedBytes">
/// Total expansion. Zip compresses, and text compresses enormously — a few megabytes of zeroes
/// reaches terabytes.
/// </param>
/// <param name="MaxRowsPerTable">Rows in a single table.</param>
/// <param name="MaxRowBytes">Length of one row.</param>
/// <param name="BatchSize">
/// How many rows are held at once while classifying or writing. Bounds the working set
/// independently of how large the archive is allowed to be.
/// </param>
public sealed record ArchiveReaderLimits(
    long MaxUncompressedBytes,
    int MaxRowsPerTable,
    long MaxRowBytes,
    int BatchSize)
{
    public static readonly ArchiveReaderLimits Default = new(
        MaxUncompressedBytes: 8L * 1024 * 1024 * 1024,
        MaxRowsPerTable: 2_000_000,
        MaxRowBytes: 4L * 1024 * 1024,
        BatchSize: 5_000);
}
