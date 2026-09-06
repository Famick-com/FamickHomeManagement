namespace Famick.HomeManagement.Core.DTOs.DataPortability;

/// <summary>
/// The index of an archive: what is in it, what was left out, and why.
/// </summary>
/// <remarks>
/// Written last, because it carries a checksum of every other entry.
/// </remarks>
public sealed class ArchiveManifest
{
    /// <summary>
    /// The shape of the archive itself — layout, manifest keys, encoding.
    /// </summary>
    /// <remarks>
    /// Deliberately not bumped when a table or column changes. The manifest declares its own
    /// columns and the reader matches by name, so ordinary schema drift stays readable. This
    /// product ships continuously; a version gate that tripped on every migration would turn
    /// routine work into a portability regression, which is the opposite of the point.
    /// </remarks>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// The oldest reader that can still make sense of this archive. Lets a future breaking
    /// change to the envelope refuse politely instead of failing halfway through.
    /// </summary>
    public int MinimumReaderVersion { get; set; } = 1;

    public DateTime GeneratedAt { get; set; }

    public ArchiveGenerator Generator { get; set; } = new();

    /// <summary>
    /// Where this archive came from. A restore checks it against the household doing the
    /// restoring and refuses anything else.
    /// </summary>
    public ArchiveSource Source { get; set; } = new();

    public ArchiveHousehold Household { get; set; } = new();

    public List<ArchiveTable> Tables { get; set; } = [];

    /// <summary>
    /// Types deliberately left out, with the reason. Part of the archive so a person can see
    /// what is not here without having to read the source.
    /// </summary>
    public List<ArchiveExclusion> ExcludedEntities { get; set; } = [];

    public List<ArchiveFile> Files { get; set; } = [];

    /// <summary>
    /// Files the database referenced that storage could not produce.
    /// </summary>
    /// <remarks>
    /// Surfaced to the user rather than logged and forgotten. Somebody who cancels an account
    /// after taking an export should not discover the gaps afterwards.
    /// </remarks>
    public List<ArchiveMissingFile> MissingFiles { get; set; } = [];

    public ArchiveCounts Counts { get; set; } = new();
}

public sealed class ArchiveGenerator
{
    public string Product { get; set; } = "Famick";
    public string? AppVersion { get; set; }
}

public sealed class ArchiveSource
{
    /// <summary>"cloud" or "selfHosted".</summary>
    public string Mode { get; set; } = string.Empty;

    public Guid TenantId { get; set; }
}

public sealed class ArchiveHousehold
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// One entity type's rows, and how to read them back.
/// </summary>
public sealed class ArchiveTable
{
    /// <summary>Position in the archive; also a valid write order.</summary>
    public int Order { get; set; }

    /// <summary>The CLR entity name, which is what a reader matches on.</summary>
    public string Entity { get; set; } = string.Empty;

    public string Table { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public string? Sha256 { get; set; }

    /// <summary>
    /// The property names present in the file. Declared per table so a reader can ignore
    /// columns it does not know and default ones it expected.
    /// </summary>
    public List<string> Columns { get; set; } = [];

    /// <summary>Columns withheld from an otherwise exported table, e.g. a password hash.</summary>
    public List<string> ExcludedColumns { get; set; } = [];

    /// <summary>"own" when the table carries TenantId, "viaParent" when it reaches one through a join.</summary>
    public string TenantScope { get; set; } = "own";

    /// <summary>The chain of entity names joined through, when TenantScope is "viaParent".</summary>
    public List<string> TenantPath { get; set; } = [];
}

public sealed record ArchiveExclusion(string Entity, string Disposition, string Reason);

public sealed class ArchiveFile
{
    /// <summary>Which kind of attachment, e.g. product-image.</summary>
    public string Kind { get; set; } = string.Empty;

    public string OwnerEntity { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Sha256 { get; set; }
    public long Bytes { get; set; }
    public string? ContentType { get; set; }
}

public sealed record ArchiveMissingFile(string Kind, Guid OwnerId, string FileName, string Reason);

public sealed class ArchiveCounts
{
    public long Rows { get; set; }
    public int Files { get; set; }
    public int MissingFiles { get; set; }
    public long Bytes { get; set; }
}
