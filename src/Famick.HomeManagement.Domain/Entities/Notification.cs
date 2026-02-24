using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Represents an in-app notification stored for a user.
/// </summary>
public class Notification : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? DismissedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
}
