using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

/// <summary>
/// Everything the "your export is ready" email needs.
/// </summary>
public class DataExportReadyData : IMessageData
{
    public string UserName { get; set; } = string.Empty;
    public string HouseholdName { get; set; } = string.Empty;

    /// <summary>Carries a signed access token, so the link works without signing in first.</summary>
    public string DownloadLink { get; set; } = string.Empty;

    public string ExpiresOn { get; set; } = string.Empty;
    public string SizeDescription { get; set; } = string.Empty;
    public long RowCount { get; set; }
    public int FileCount { get; set; }

    /// <summary>
    /// How many referenced files storage could not produce.
    /// </summary>
    /// <remarks>
    /// In the email rather than only in the app, because someone taking an export before closing
    /// their account may never open the app again.
    /// </remarks>
    public int MissingFileCount { get; set; }

    public bool HasMissingFiles => MissingFileCount > 0;
}
