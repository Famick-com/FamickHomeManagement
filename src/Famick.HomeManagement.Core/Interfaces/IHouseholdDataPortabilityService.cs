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
    /// <returns>Null when there is no such archive, or it has expired.</returns>
    Task<ExportDownload?> OpenArchiveAsync(Guid transferId, long? rangeStart, long? rangeEnd, CancellationToken ct = default);

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
    Task RunExportAsync(Guid transferId, CancellationToken ct = default);
}

/// <summary>An archive opened for reading, with what the response needs to describe it.</summary>
public sealed record ExportDownload(Stream Content, string FileName, long TotalLength, long? RangeStart, long? RangeEnd);
