namespace Famick.HomeManagement.Core.DTOs.Notifications;

public class NotificationListRequest
{
    public bool? ReadFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
