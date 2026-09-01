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

    /// <summary>
    /// How many stock entries this line stands for.
    /// </summary>
    /// <remarks>
    /// Buying two of something makes two stock entries, and listing both produces two
    /// identical lines — same name, same date, same place. In a household with a full
    /// pantry that doubles the length of the email without adding anything to read.
    /// </remarks>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Rendered as "× 2" when a line stands for more than one entry, and empty otherwise,
    /// so the common case reads as an ordinary list.
    /// </summary>
    public string QuantitySuffix => Quantity > 1 ? $" × {Quantity}" : string.Empty;

    /// <summary>
    /// Days from today until the best-before date; negative once past it. Set by the evaluator,
    /// which is the only place that knows what "today" is.
    /// </summary>
    public int DaysUntilExpiry { get; set; }

    /// <summary>
    /// How long is left, in words.
    /// <para>
    /// What a reader actually needs from this message is whether to act now, and "in 2 days"
    /// answers that where a calendar date makes them work it out. The exact date stays
    /// available in the app for anyone who wants it.
    /// </para>
    /// </summary>
    public string RelativeExpiry => DaysUntilExpiry switch
    {
        < -1 => $"Expired {-DaysUntilExpiry} days ago",
        -1 => "Expired yesterday",
        0 => "Expires today",
        1 => "Expires tomorrow",
        _ => $"Expires in {DaysUntilExpiry} days"
    };
}
