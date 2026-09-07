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

    // Singular and plural chosen per count rather than per email, so a one-item export does not
    // read "1 records". Mustache has no conditionals beyond truthiness, so the decision is made
    // here and the template just picks a branch.
    public bool HasOneRow => RowCount == 1;
    public bool HasOneFile => FileCount == 1;
    public bool HasOneMissingFile => MissingFileCount == 1;
}
