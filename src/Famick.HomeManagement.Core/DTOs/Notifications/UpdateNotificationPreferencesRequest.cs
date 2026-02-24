namespace Famick.HomeManagement.Core.DTOs.Notifications;

public class UpdateNotificationPreferencesRequest
{
    public List<NotificationPreferenceDto> Preferences { get; set; } = new();
}
