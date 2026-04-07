using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class PasswordChangedData : IMessageData
{
    public string UserName { get; set; } = string.Empty;
}
