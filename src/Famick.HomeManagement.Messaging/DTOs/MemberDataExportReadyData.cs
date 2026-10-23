using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

/// <summary>
/// Everything the email carrying a departing member's own data needs.
/// </summary>
public class MemberDataExportReadyData : IMessageData
{
    public string UserName { get; set; } = string.Empty;
    public string HouseholdName { get; set; } = string.Empty;

    /// <summary>Carries a signed access token: by the time it is opened there is no account to sign in to.</summary>
    public string DownloadLink { get; set; } = string.Empty;

    public string ExpiresOn { get; set; } = string.Empty;
    public int ContactCount { get; set; }

    public bool HasOneContact => ContactCount == 1;
}
