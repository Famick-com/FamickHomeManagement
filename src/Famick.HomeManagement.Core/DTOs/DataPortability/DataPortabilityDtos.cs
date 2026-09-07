namespace Famick.HomeManagement.Core.DTOs.DataPortability;

/// <summary>
/// What this deployment can do, so the client can show the right thing rather than a button
/// that fails.
/// </summary>
public sealed class DataPortabilityCapabilities
{
    public bool ExportSupported { get; set; } = true;

    /// <summary>False until restore ships; the UI hides its entry point rather than 404ing.</summary>
    public bool RestoreSupported { get; set; }

    public long MaxUploadBytes { get; set; }

    /// <summary>An export already running, so the UI can resume watching it after a reload.</summary>
    public Guid? ActiveExportId { get; set; }

    /// <summary>The most recent finished export still inside its expiry.</summary>
    public DataExportSummary? LatestExport { get; set; }
}

/// <summary>
/// One export, as the UI sees it.
/// </summary>
public sealed class DataExportSummary
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }

    public string? ProgressLabel { get; set; }
    public long ProgressCurrent { get; set; }
    public long ProgressTotal { get; set; }

    /// <summary>0–100, or null before there is anything to report.</summary>
    public int? PercentComplete =>
        ProgressTotal <= 0 ? null : (int)Math.Clamp(ProgressCurrent * 100 / ProgressTotal, 0, 100);

    public string? FileName { get; set; }
    public long? Bytes { get; set; }

    public long RowCount { get; set; }
    public int FileCount { get; set; }

    /// <summary>
    /// Referenced files storage could not produce. Surfaced rather than buried — someone taking
    /// an export before closing their account should not find the gaps later.
    /// </summary>
    public int MissingFileCount { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public bool IsDownloadable =>
        Status == "Completed" && ExpiresAt.HasValue && ExpiresAt.Value > DateTime.UtcNow;
}

/// <summary>Options when asking for an export.</summary>
public sealed class StartExportRequest
{
    /// <summary>
    /// Photos and documents. On by default: an archive without them is not a backup, and the
    /// people most likely to turn it off are the ones who least understand the consequence.
    /// </summary>
    public bool IncludeFiles { get; set; } = true;
}
