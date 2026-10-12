using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class AccountAlreadyExistsData : IMessageData
{
    public string LoginLink { get; set; } = string.Empty;
    public string ForgotPasswordLink { get; set; } = string.Empty;
}
