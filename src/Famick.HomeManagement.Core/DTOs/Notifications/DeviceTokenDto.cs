using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Notifications;

public class DeviceTokenDto
{
    public Guid Id { get; set; }
    public DevicePlatform Platform { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
