using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class FeatureAnnouncementData : IMessageData
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
    public bool IsSingle { get; set; }
    public List<AnnouncementItemData> Announcements { get; set; } = [];
}

public class AnnouncementItemData
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool HasLink => !string.IsNullOrEmpty(LinkUrl);
}
