using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class HouseholdInvitationData : IMessageData
{
    public string UserName { get; set; } = string.Empty;
    public string HouseholdName { get; set; } = string.Empty;
    public string InvitedByName { get; set; } = string.Empty;
    public string AcceptLink { get; set; } = string.Empty;
    public int ExpiresInDays { get; set; }
}
