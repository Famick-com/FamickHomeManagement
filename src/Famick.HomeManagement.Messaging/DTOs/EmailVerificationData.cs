using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class EmailVerificationData : IMessageData
{
    public string HouseholdName { get; set; } = string.Empty;
    public string VerificationLink { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
