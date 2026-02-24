using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Per-user channel preferences for a specific notification type.
/// </summary>
public class NotificationPreference : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public NotificationType NotificationType { get; set; }
    public bool EmailEnabled { get; set; } = true;
    public bool PushEnabled { get; set; } = true;
    public bool InAppEnabled { get; set; } = true;

    // Navigation
    public User User { get; set; } = null!;
}
