using Famick.HomeManagement.Core.DTOs.DataPortability;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Exporting a household's data, and getting it back.
/// </summary>
public interface IHouseholdDataPortabilityService
{
    Task<DataPortabilityCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Queues an export for the current household.
    /// </summary>
    /// <remarks>
    /// Returns the run already in flight if there is one, rather than starting a second. Two
    /// concurrent exports of the same household produce two large archives to no purpose.
    /// </remarks>
    Task<DataExportSummary> StartExportAsync(StartExportRequest request, Guid requestedByUserId, CancellationToken ct = default);

    /// <summary>Progress, read from the database rather than from a worker's memory.</summary>
    Task<DataExportSummary?> GetExportAsync(Guid transferId, CancellationToken ct = default);

    Task<ArchiveManifest?> GetManifestAsync(Guid transferId, CancellationToken ct = default);

    /// <summary>
    /// Opens a finished archive for download, honouring a byte range.
    /// </summary>
    /// <remarks>
    /// Ranges are resolved here rather than by the caller, because normalising one needs the
    /// archive's length: a suffix range asks for the last N bytes, and whether a start lies past
    /// the end cannot be known without it.
    /// </remarks>
    Task<ExportDownloadResult> OpenArchiveAsync(Guid transferId, long? rangeStart, long? rangeEnd, CancellationToken ct = default);

    /// <summary>
    /// A download URL carrying a short-lived signed token.
    /// </summary>
    /// <remarks>
    /// The browser cannot send the app's bearer token on a plain navigation, and the archive is
    /// far too large to pull through JavaScript as a blob. So the client asks for a signed URL and
    /// opens that. The token is short-lived because, unlike the emailed one, it is only needed for
    /// the moment between the click and the download starting.
    /// </remarks>
    Task<string?> GetDownloadLinkAsync(Guid transferId, CancellationToken ct = default);

    /// <summary>Deletes an archive at the user's request, before it would expire on its own.</summary>
    Task<bool> DeleteExportAsync(Guid transferId, CancellationToken ct = default);

    /// <summary>
    /// Runs one queued export to completion. Called by the background worker.
    /// </summary>
    /// <returns>
    /// True when this call claimed the transfer and ran it. False when somebody else already had
    /// it — the worker needs to tell those apart, or it spins on a row it will never get.
    /// </returns>
    Task<bool> RunExportAsync(Guid transferId, CancellationToken ct = default);
}

/// <summary>An archive opened for reading, with what the response needs to describe it.</summary>
public sealed record ExportDownload(Stream Content, string FileName, long TotalLength, long? RangeStart, long? RangeEnd);

/// <summary>
/// The outcome of asking for an archive.
/// </summary>
/// <remarks>
/// "Not there" and "you asked for bytes that do not exist" are different answers and the client
/// should be told which: one means request a new export, the other means fix the request.
/// </remarks>
public sealed record ExportDownloadResult(ExportDownloadStatus Status, ExportDownload? Download = null)
{
    public static readonly ExportDownloadResult Unavailable = new(ExportDownloadStatus.Unavailable);
    public static readonly ExportDownloadResult RangeNotSatisfiable = new(ExportDownloadStatus.RangeNotSatisfiable);

    public static ExportDownloadResult Ok(ExportDownload download) => new(ExportDownloadStatus.Ok, download);
}

public enum ExportDownloadStatus
{
    Ok,

    /// <summary>No such archive, or it has expired.</summary>
    Unavailable,

    /// <summary>The requested range lies outside the archive.</summary>
    RangeNotSatisfiable,
}
