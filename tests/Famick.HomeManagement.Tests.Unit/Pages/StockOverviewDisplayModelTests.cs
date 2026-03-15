using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Unit tests for StockOverviewDisplayModel logic used in StockOverviewPage.
/// Tests the display logic for expiry text, status flags, and due-soon-only computation.
///
/// Note: These tests recreate the display model logic to avoid MAUI project dependency.
/// The actual implementation is in Famick.HomeManagement.Mobile.Pages.StockOverviewPage.cs
/// </summary>
public class StockOverviewDisplayModelTests
{
    #region ExpiryDisplayText Tests

    [Fact]
    public void ExpiryDisplayText_WithNoNextDueDate_ReturnsEmpty()
    {
        var model = CreateModel(nextDueDate: null);
        model.ExpiryDisplayText.Should().Be("");
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpired_ReturnsExpiredWithDate()
    {
        var expiredDate = DateTime.UtcNow.Date.AddDays(-5);
        var model = CreateModel(nextDueDate: expiredDate, isExpired: true);

        model.ExpiryDisplayText.Should().StartWith("Expired ");
        model.ExpiryDisplayText.Should().Contain(expiredDate.ToString("MMM d"));
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpiresToday_ReturnsExpiresToday()
    {
        var model = CreateModel(nextDueDate: DateTime.UtcNow.Date, daysUntilDue: 0);
        model.ExpiryDisplayText.Should().Be("Expires today");
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpiresTomorrow_ReturnsExpiresTomorrow()
    {
        var model = CreateModel(nextDueDate: DateTime.UtcNow.Date.AddDays(1), daysUntilDue: 1);
        model.ExpiryDisplayText.Should().Be("Expires tomorrow");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(7)]
    public void ExpiryDisplayText_WhenExpiresWithinWeek_ReturnsDaysFormat(int days)
    {
        var model = CreateModel(nextDueDate: DateTime.UtcNow.Date.AddDays(days), daysUntilDue: days);
        model.ExpiryDisplayText.Should().Be($"Expires in {days}d");
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpiresMoreThanWeekAway_ReturnsFormattedDate()
    {
        var futureDate = DateTime.UtcNow.Date.AddDays(14);
        var model = CreateModel(nextDueDate: futureDate, daysUntilDue: 14);

        model.ExpiryDisplayText.Should().StartWith("Expires ");
        model.ExpiryDisplayText.Should().Contain(futureDate.ToString("MMM d"));
    }

    #endregion

    #region Status Flag Tests

    [Fact]
    public void IsDueSoonOnly_WhenDueSoonAndNotExpired_ReturnsTrue()
    {
        var model = CreateModel(isDueSoon: true, isExpired: false);
        model.IsDueSoonOnly.Should().BeTrue();
    }

    [Fact]
    public void IsDueSoonOnly_WhenDueSoonAndExpired_ReturnsFalse()
    {
        var model = CreateModel(isDueSoon: true, isExpired: true);
        model.IsDueSoonOnly.Should().BeFalse();
    }

    [Fact]
    public void IsDueSoonOnly_WhenNotDueSoon_ReturnsFalse()
    {
        var model = CreateModel(isDueSoon: false, isExpired: false);
        model.IsDueSoonOnly.Should().BeFalse();
    }

    [Fact]
    public void HasImage_WithUrl_ReturnsTrue()
    {
        var model = CreateModel(primaryImageUrl: "https://example.com/image.jpg");
        model.HasImage.Should().BeTrue();
    }

    [Fact]
    public void HasImage_WithNullUrl_ReturnsFalse()
    {
        var model = CreateModel(primaryImageUrl: null);
        model.HasImage.Should().BeFalse();
    }

    [Fact]
    public void HasImage_WithEmptyUrl_ReturnsFalse()
    {
        var model = CreateModel(primaryImageUrl: "");
        model.HasImage.Should().BeFalse();
    }

    #endregion

    #region Filter Logic Tests

    [Fact]
    public void FilterLogic_ExpiredFilter_OnlyIncludesExpiredItems()
    {
        var items = new List<TestStockOverviewItem>
        {
            CreateModel(productName: "Expired Milk", isExpired: true),
            CreateModel(productName: "Fresh Bread", isExpired: false),
            CreateModel(productName: "Expired Cheese", isExpired: true),
        };

        var filtered = items.Where(i => i.IsExpired).ToList();
        filtered.Should().HaveCount(2);
        filtered.Select(i => i.ProductName).Should().Contain("Expired Milk", "Expired Cheese");
    }

    [Fact]
    public void FilterLogic_DueSoonFilter_ExcludesExpiredItems()
    {
        var items = new List<TestStockOverviewItem>
        {
            CreateModel(productName: "Expired", isDueSoon: true, isExpired: true),
            CreateModel(productName: "Due Soon", isDueSoon: true, isExpired: false),
            CreateModel(productName: "Fresh", isDueSoon: false, isExpired: false),
        };

        var filtered = items.Where(i => i.IsDueSoon && !i.IsExpired).ToList();
        filtered.Should().HaveCount(1);
        filtered[0].ProductName.Should().Be("Due Soon");
    }

    [Fact]
    public void FilterLogic_BelowMinFilter_OnlyIncludesBelowMin()
    {
        var items = new List<TestStockOverviewItem>
        {
            CreateModel(productName: "Low Stock", isBelowMinStock: true),
            CreateModel(productName: "OK Stock", isBelowMinStock: false),
        };

        var filtered = items.Where(i => i.IsBelowMinStock).ToList();
        filtered.Should().HaveCount(1);
        filtered[0].ProductName.Should().Be("Low Stock");
    }

    #endregion

    #region Test Helpers

    private static TestStockOverviewItem CreateModel(
        string productName = "Test Product",
        DateTime? nextDueDate = null,
        int daysUntilDue = 0,
        bool isExpired = false,
        bool isDueSoon = false,
        bool isBelowMinStock = false,
        string? primaryImageUrl = null)
    {
        return new TestStockOverviewItem
        {
            ProductId = Guid.NewGuid(),
            ProductName = productName,
            TotalAmount = 1.0m,
            QuantityUnitName = "pc",
            NextDueDate = nextDueDate,
            DaysUntilDue = daysUntilDue,
            IsExpired = isExpired,
            IsDueSoon = isDueSoon,
            IsBelowMinStock = isBelowMinStock,
            PrimaryImageUrl = primaryImageUrl,
        };
    }

    /// <summary>
    /// Mirrors StockOverviewDisplayModel logic from StockOverviewPage.xaml.cs
    /// </summary>
    private class TestStockOverviewItem
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public string QuantityUnitName { get; set; } = "";
        public DateTime? NextDueDate { get; set; }
        public int DaysUntilDue { get; set; }
        public bool IsExpired { get; set; }
        public bool IsDueSoon { get; set; }
        public bool IsBelowMinStock { get; set; }
        public string? PrimaryImageUrl { get; set; }

        public bool IsDueSoonOnly => IsDueSoon && !IsExpired;
        public bool HasImage => !string.IsNullOrEmpty(PrimaryImageUrl);

        public string ExpiryDisplayText
        {
            get
            {
                if (!NextDueDate.HasValue)
                    return "";

                if (IsExpired)
                    return $"Expired {NextDueDate.Value:MMM d}";

                if (DaysUntilDue <= 0)
                    return "Expires today";

                if (DaysUntilDue == 1)
                    return "Expires tomorrow";

                if (DaysUntilDue <= 7)
                    return $"Expires in {DaysUntilDue}d";

                return $"Expires {NextDueDate.Value:MMM d}";
            }
        }
    }

    #endregion
}
