using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Notifications;

public class NotificationPreferenceDto
{
    public NotificationType NotificationType { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool EmailEnabled { get; set; }
    public bool PushEnabled { get; set; }
    public bool InAppEnabled { get; set; }
}
