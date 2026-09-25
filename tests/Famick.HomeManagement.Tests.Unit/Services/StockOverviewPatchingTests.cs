using Famick.HomeManagement.Core.DTOs.Stock;
using Famick.HomeManagement.UI.Services;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// The arithmetic behind patching one stock-overview row in place. Worth testing on its own
/// because the statistics header is the part a bad patch breaks silently — nothing re-checks it
/// until the next full load.
/// </summary>
public class StockOverviewPatchingTests
{
    private static StockOverviewItemDto Row(
        bool expired = false,
        bool dueSoon = false,
        bool belowMin = false,
        decimal totalValue = 10m) => new()
        {
            ProductId = Guid.NewGuid(),
            ProductName = "Row",
            IsExpired = expired,
            IsDueSoon = dueSoon,
            IsBelowMinStock = belowMin,
            TotalValue = totalValue
        };

    private static StockStatisticsDto Stats(
        int products = 10, decimal value = 100m,
        int expired = 2, int dueSoon = 3, int belowMin = 4, int overdue = 0) => new()
        {
            TotalProductCount = products,
            TotalStockValue = value,
            ExpiredCount = expired,
            DueSoonCount = dueSoon,
            BelowMinStockCount = belowMin,
            OverdueCount = overdue
        };

    #region MatchesStatus

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("overdue")]  // the server's own filter ignores this one, so nothing is excluded
    public void MatchesStatus_WithNoEffectiveFilter_AlwaysMatches(string? status)
    {
        StockOverviewPatching.MatchesStatus(Row(), status).Should().BeTrue();
    }

    [Theory]
    [InlineData("expired", true, false, false, true)]
    [InlineData("expired", false, true, true, false)]
    [InlineData("duesoon", false, true, false, true)]
    [InlineData("duesoon", true, false, true, false)]
    [InlineData("belowminstock", false, false, true, true)]
    [InlineData("belowminstock", true, true, false, false)]
    public void MatchesStatus_TestsTheFlagTheFilterNames(
        string status, bool expired, bool dueSoon, bool belowMin, bool expected)
    {
        StockOverviewPatching
            .MatchesStatus(Row(expired, dueSoon, belowMin), status)
            .Should().Be(expected);
    }

    [Fact]
    public void MatchesStatus_IsCaseInsensitive()
    {
        // The status arrives from a query string, so casing is not ours to rely on.
        StockOverviewPatching.MatchesStatus(Row(expired: true), "Expired").Should().BeTrue();
        StockOverviewPatching.MatchesStatus(Row(dueSoon: true), "DueSoon").Should().BeTrue();
        StockOverviewPatching.MatchesStatus(Row(belowMin: true), "BelowMinStock").Should().BeTrue();
    }

    #endregion

    #region ApplyRowDelta

    [Fact]
    public void ApplyRowDelta_WhenNothingAboutTheRowChanged_LeavesStatisticsAlone()
    {
        var stats = Stats();
        var row = Row(dueSoon: true, totalValue: 12m);

        StockOverviewPatching.ApplyRowDelta(stats, row, row);

        stats.Should().BeEquivalentTo(Stats());
    }

    [Fact]
    public void ApplyRowDelta_WhenARowVanishes_DropsItsContribution()
    {
        var stats = Stats();
        var before = Row(expired: true, belowMin: true, totalValue: 15m);

        StockOverviewPatching.ApplyRowDelta(stats, before, null);

        stats.TotalProductCount.Should().Be(9);
        stats.TotalStockValue.Should().Be(85m);
        stats.ExpiredCount.Should().Be(1);
        stats.DueSoonCount.Should().Be(3, "the row was not due soon");
        stats.BelowMinStockCount.Should().Be(3);
    }

    [Fact]
    public void ApplyRowDelta_WhenARowAppears_AddsItsContribution()
    {
        var stats = Stats();
        var after = Row(dueSoon: true, totalValue: 7m);

        StockOverviewPatching.ApplyRowDelta(stats, null, after);

        stats.TotalProductCount.Should().Be(11);
        stats.TotalStockValue.Should().Be(107m);
        stats.DueSoonCount.Should().Be(4);
        stats.ExpiredCount.Should().Be(2);
        stats.BelowMinStockCount.Should().Be(4);
    }

    [Fact]
    public void ApplyRowDelta_WhenFlagsFlip_MovesEachCountOnce()
    {
        var stats = Stats();
        var before = Row(expired: true, dueSoon: false, belowMin: false, totalValue: 20m);
        var after = Row(expired: false, dueSoon: true, belowMin: true, totalValue: 5m);

        StockOverviewPatching.ApplyRowDelta(stats, before, after);

        stats.TotalProductCount.Should().Be(10, "the row still exists");
        stats.TotalStockValue.Should().Be(85m);
        stats.ExpiredCount.Should().Be(1);
        stats.DueSoonCount.Should().Be(4);
        stats.BelowMinStockCount.Should().Be(5);
    }

    [Fact]
    public void ApplyRowDelta_NeverTouchesOverdueCount()
    {
        // The server hardcodes OverdueCount to 0, so inventing a delta for it would only drift.
        var stats = Stats(overdue: 0);

        StockOverviewPatching.ApplyRowDelta(stats, Row(expired: true), null);

        stats.OverdueCount.Should().Be(0);
    }

    [Fact]
    public void ApplyRowDelta_WithNoStatisticsLoaded_DoesNothing()
    {
        // The header may not have arrived yet; a tap must not throw because of it.
        var act = () => StockOverviewPatching.ApplyRowDelta(null, Row(), null);

        act.Should().NotThrow();
    }

    [Fact]
    public void ApplyRowDelta_AppliedStepwise_MatchesOneDeltaOverTheWholeChange()
    {
        // Why the page needs no in-flight guard: consume/add are incremental, so two quick taps
        // are two real actions. Each patch measures from the list as it stands at that moment, so
        // chaining them lands where a single combined change would.
        var stepwise = Stats();
        var three = Row(belowMin: false, totalValue: 30m);
        var two = Row(belowMin: false, totalValue: 20m);
        var one = Row(belowMin: true, totalValue: 10m);

        StockOverviewPatching.ApplyRowDelta(stepwise, three, two);
        StockOverviewPatching.ApplyRowDelta(stepwise, two, one);

        var atOnce = Stats();
        StockOverviewPatching.ApplyRowDelta(atOnce, three, one);

        stepwise.Should().BeEquivalentTo(atOnce);
        stepwise.TotalStockValue.Should().Be(80m);
        stepwise.BelowMinStockCount.Should().Be(5);
    }

    [Fact]
    public void ApplyRowDelta_PatchingDownToNothing_MatchesTheSingleStepEquivalent()
    {
        var stepwise = Stats();
        var two = Row(dueSoon: true, totalValue: 20m);
        var one = Row(dueSoon: true, totalValue: 10m);

        StockOverviewPatching.ApplyRowDelta(stepwise, two, one);
        StockOverviewPatching.ApplyRowDelta(stepwise, one, null);

        var atOnce = Stats();
        StockOverviewPatching.ApplyRowDelta(atOnce, two, null);

        stepwise.Should().BeEquivalentTo(atOnce);
        stepwise.TotalProductCount.Should().Be(9);
        stepwise.DueSoonCount.Should().Be(2);
        stepwise.TotalStockValue.Should().Be(80m);
    }

    #endregion
}
