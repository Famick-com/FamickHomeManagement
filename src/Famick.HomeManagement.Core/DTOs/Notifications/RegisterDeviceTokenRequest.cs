using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Notifications;

public class RegisterDeviceTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
}
