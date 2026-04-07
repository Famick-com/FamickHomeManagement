using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class CalendarReminderData : IMessageData
{
    public string EventTitle { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
}
