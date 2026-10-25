using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class AdminPasswordResetData : IMessageData
{
    public string UserName { get; set; } = string.Empty;
    public string ResetLink { get; set; } = string.Empty;
    public int ExpiresInHours { get; set; }
}
