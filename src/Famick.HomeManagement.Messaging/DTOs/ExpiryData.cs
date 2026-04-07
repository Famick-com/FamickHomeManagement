using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class ExpiryData : IMessageData
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
    public int ExpiredCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public List<ExpiryItemData> ExpiringItems { get; set; } = [];

    public bool HasExpired => ExpiredCount > 0;
    public bool HasExpiringSoon => ExpiringSoonCount > 0;
}

public class ExpiryItemData
{
    public string ProductName { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public string Status => IsExpired ? "Expired" : "Expiring soon";
}
