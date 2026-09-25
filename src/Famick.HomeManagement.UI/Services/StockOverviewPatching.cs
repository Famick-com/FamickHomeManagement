using Famick.HomeManagement.Core.DTOs.Stock;

namespace Famick.HomeManagement.UI.Services;

/// <summary>
/// Pure helpers for patching a single stock-overview row in place instead of reloading the list.
///
/// These live outside the page so the arithmetic is testable on its own: the statistics header is
/// the one thing a patch can silently get wrong, since nothing on the server re-checks it until
/// the next full load.
/// </summary>
public static class StockOverviewPatching
{
    /// <summary>
    /// Whether a row still belongs in a list narrowed to <paramref name="status"/>.
    ///
    /// The server applies this filter after building rows, so a row that changed in place has to be
    /// re-tested client-side. "overdue" always matches because the server's own filter ignores it.
    /// </summary>
    public static bool MatchesStatus(StockOverviewItemDto item, string? status) =>
        status?.ToLowerInvariant() switch
        {
            "expired" => item.IsExpired,
            "duesoon" => item.IsDueSoon,
            "belowminstock" => item.IsBelowMinStock,
            _ => true
        };

    /// <summary>
    /// Moves <paramref name="statistics"/> by the difference one row made.
    ///
    /// Exact rather than approximate: the server's statistics are counts of rows over the very same
    /// predicates each row DTO carries, so adding the delta for the one row that changed lands on
    /// the number a full recount would give. Either side may be null — <paramref name="before"/>
    /// when a row appears, <paramref name="after"/> when it is gone.
    ///
    /// OverdueCount is deliberately untouched: the server hardcodes it to zero.
    /// </summary>
    public static void ApplyRowDelta(
        StockStatisticsDto? statistics,
        StockOverviewItemDto? before,
        StockOverviewItemDto? after)
    {
        if (statistics == null)
        {
            return;
        }

        statistics.TotalProductCount += Count(after) - Count(before);
        statistics.TotalStockValue += (after?.TotalValue ?? 0m) - (before?.TotalValue ?? 0m);
        statistics.ExpiredCount += Flag(after?.IsExpired) - Flag(before?.IsExpired);
        statistics.DueSoonCount += Flag(after?.IsDueSoon) - Flag(before?.IsDueSoon);
        statistics.BelowMinStockCount += Flag(after?.IsBelowMinStock) - Flag(before?.IsBelowMinStock);
    }

    private static int Count(StockOverviewItemDto? item) => item == null ? 0 : 1;

    private static int Flag(bool? value) => value == true ? 1 : 0;
}
