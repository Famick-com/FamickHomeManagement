using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class HouseholdInvitationExistingAccountData : IMessageData
{
    public string HouseholdName { get; set; } = string.Empty;
    public string InvitedByName { get; set; } = string.Empty;
    public string LoginLink { get; set; } = string.Empty;
}
