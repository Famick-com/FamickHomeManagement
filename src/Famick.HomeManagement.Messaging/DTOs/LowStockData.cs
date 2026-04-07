using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Messaging.DTOs;

public class LowStockData : IMessageData
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string? DeepLinkUrl { get; set; }
    public int ItemCount { get; set; }
    public List<LowStockItemData> LowStockItems { get; set; } = [];
}

public class LowStockItemData
{
    public string Name { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal MinStockAmount { get; set; }
}
