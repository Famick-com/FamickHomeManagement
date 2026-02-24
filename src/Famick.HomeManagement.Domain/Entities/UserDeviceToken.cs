using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Push notification device token registration (cloud only).
/// </summary>
public class UserDeviceToken : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DevicePlatform Platform { get; set; }
    public DateTime? LastUsedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
