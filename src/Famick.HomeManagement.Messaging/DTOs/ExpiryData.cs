using System.Linq;
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

    /// <summary>
    /// The two groups, split for templates that present them separately.
    /// <para>
    /// Grouping is what lets a message distinguish already-expired from expiring-soon without
    /// relying on colour, which several mail clients strip and some readers cannot see.
    /// Ordering within each group is inherited from <see cref="ExpiringItems"/>, which the
    /// evaluator already sorts by date.
    /// </para>
    /// </summary>
    public IEnumerable<ExpiryItemData> ExpiredItems => ExpiringItems.Where(i => i.IsExpired);

    /// <inheritdoc cref="ExpiredItems"/>
    public IEnumerable<ExpiryItemData> ExpiringSoonItems => ExpiringItems.Where(i => !i.IsExpired);
}

public class ExpiryItemData
{
    public string ProductName { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public bool IsExpired { get; set; }
    public string Status => IsExpired ? "Expired" : "Expiring soon";
}
