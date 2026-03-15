using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for dashboard stat card conditional visibility logic.
/// Mirrors the visibility rules from DashboardPage.xaml.cs UpdateUI().
///
/// Note: These tests recreate the visibility logic to avoid MAUI project dependency.
/// </summary>
public class DashboardCardVisibilityTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void ShoppingCard_VisibleOnlyWhenCountGreaterThanZero(int count, bool expectedVisible)
    {
        var visibility = ComputeVisibility(shoppingCount: count);
        visibility.ShoppingCardVisible.Should().Be(expectedVisible);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    public void LowStockCard_VisibleOnlyWhenCountGreaterThanZero(int count, bool expectedVisible)
    {
        var visibility = ComputeVisibility(lowStockCount: count);
        visibility.LowStockCardVisible.Should().Be(expectedVisible);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    public void ChoresCard_VisibleOnlyWhenTotalDueGreaterThanZero(int totalDue, bool expectedVisible)
    {
        var visibility = ComputeVisibility(totalChoresDue: totalDue);
        visibility.ChoresCardVisible.Should().Be(expectedVisible);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(7, true)]
    public void ExpiringCard_VisibleOnlyWhenCountGreaterThanZero(int count, bool expectedVisible)
    {
        var visibility = ComputeVisibility(dueSoonCount: count);
        visibility.ExpiringCardVisible.Should().Be(expectedVisible);
    }

    [Fact]
    public void StatsRow1_HiddenWhenBothCardsHidden()
    {
        var visibility = ComputeVisibility(shoppingCount: 0, lowStockCount: 0);
        visibility.StatsRow1Visible.Should().BeFalse();
    }

    [Fact]
    public void StatsRow1_VisibleWhenShoppingCardVisible()
    {
        var visibility = ComputeVisibility(shoppingCount: 3, lowStockCount: 0);
        visibility.StatsRow1Visible.Should().BeTrue();
    }

    [Fact]
    public void StatsRow1_VisibleWhenLowStockCardVisible()
    {
        var visibility = ComputeVisibility(shoppingCount: 0, lowStockCount: 2);
        visibility.StatsRow1Visible.Should().BeTrue();
    }

    [Fact]
    public void StatsRow2_HiddenWhenBothCardsHidden()
    {
        var visibility = ComputeVisibility(totalChoresDue: 0, dueSoonCount: 0);
        visibility.StatsRow2Visible.Should().BeFalse();
    }

    [Fact]
    public void StatsRow2_VisibleWhenChoresCardVisible()
    {
        var visibility = ComputeVisibility(totalChoresDue: 1, dueSoonCount: 0);
        visibility.StatsRow2Visible.Should().BeTrue();
    }

    [Fact]
    public void StatsRow2_VisibleWhenExpiringCardVisible()
    {
        var visibility = ComputeVisibility(totalChoresDue: 0, dueSoonCount: 5);
        visibility.StatsRow2Visible.Should().BeTrue();
    }

    [Fact]
    public void AllZero_AllCardsAndRowsHidden()
    {
        var visibility = ComputeVisibility(
            shoppingCount: 0, lowStockCount: 0,
            totalChoresDue: 0, dueSoonCount: 0);

        visibility.ShoppingCardVisible.Should().BeFalse();
        visibility.LowStockCardVisible.Should().BeFalse();
        visibility.ChoresCardVisible.Should().BeFalse();
        visibility.ExpiringCardVisible.Should().BeFalse();
        visibility.StatsRow1Visible.Should().BeFalse();
        visibility.StatsRow2Visible.Should().BeFalse();
    }

    [Fact]
    public void AllPositive_AllCardsAndRowsVisible()
    {
        var visibility = ComputeVisibility(
            shoppingCount: 3, lowStockCount: 5,
            totalChoresDue: 2, dueSoonCount: 4);

        visibility.ShoppingCardVisible.Should().BeTrue();
        visibility.LowStockCardVisible.Should().BeTrue();
        visibility.ChoresCardVisible.Should().BeTrue();
        visibility.ExpiringCardVisible.Should().BeTrue();
        visibility.StatsRow1Visible.Should().BeTrue();
        visibility.StatsRow2Visible.Should().BeTrue();
    }

    #region Test Helpers

    /// <summary>
    /// Mirrors the visibility computation from DashboardPage.xaml.cs UpdateUI()
    /// </summary>
    private static DashboardVisibility ComputeVisibility(
        int shoppingCount = 0, int lowStockCount = 0,
        int totalChoresDue = 0, int dueSoonCount = 0)
    {
        var shoppingVisible = shoppingCount > 0;
        var lowStockVisible = lowStockCount > 0;
        var choresVisible = totalChoresDue > 0;
        var expiringVisible = dueSoonCount > 0;

        return new DashboardVisibility
        {
            ShoppingCardVisible = shoppingVisible,
            LowStockCardVisible = lowStockVisible,
            ChoresCardVisible = choresVisible,
            ExpiringCardVisible = expiringVisible,
            StatsRow1Visible = shoppingVisible || lowStockVisible,
            StatsRow2Visible = choresVisible || expiringVisible,
        };
    }

    private class DashboardVisibility
    {
        public bool ShoppingCardVisible { get; set; }
        public bool LowStockCardVisible { get; set; }
        public bool ChoresCardVisible { get; set; }
        public bool ExpiringCardVisible { get; set; }
        public bool StatsRow1Visible { get; set; }
        public bool StatsRow2Visible { get; set; }
    }

    #endregion
}
