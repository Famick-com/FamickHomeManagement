using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Unit tests for StockEntryDisplayModel logic used in QuickConsumePage.
/// Tests the display logic for expiry dates and colors.
///
/// Note: These tests recreate the display model logic to avoid MAUI project dependency.
/// The actual implementation is in Famick.HomeManagement.Mobile.Pages.QuickConsumePage.cs
/// </summary>
public class StockEntryDisplayModelTests
{
    #region ExpiryDisplayText Tests

    [Fact]
    public void ExpiryDisplayText_WithNoExpiryDate_ReturnsNoExpiryMessage()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: null);

        // Act
        var result = model.ExpiryDisplayText;

        // Assert
        result.Should().Be("No expiry date");
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpired_ReturnsExpiredMessage()
    {
        // Arrange
        var expiredDate = DateTime.UtcNow.Date.AddDays(-5);
        var model = new TestStockEntryDisplayModel(bestBeforeDate: expiredDate);

        // Act
        var result = model.ExpiryDisplayText;

        // Assert
        result.Should().StartWith("EXPIRED");
        result.Should().Contain(expiredDate.ToString("MMM d, yyyy"));
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpiresToday_ReturnsExpiresToday()
    {
        // Arrange
        var today = DateTime.UtcNow.Date;
        var model = new TestStockEntryDisplayModel(bestBeforeDate: today);

        // Act
        var result = model.ExpiryDisplayText;

        // Assert
        result.Should().Be("Expires today");
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpiresTomorrow_ReturnsExpiresTomorrow()
    {
        // Arrange
        var tomorrow = DateTime.UtcNow.Date.AddDays(1);
        var model = new TestStockEntryDisplayModel(bestBeforeDate: tomorrow);

        // Act
        var result = model.ExpiryDisplayText;

        // Assert
        result.Should().Be("Expires tomorrow");
    }

    [Theory]
    [InlineData(2, "Expires in 2 days")]
    [InlineData(3, "Expires in 3 days")]
    [InlineData(5, "Expires in 5 days")]
    [InlineData(7, "Expires in 7 days")]
    public void ExpiryDisplayText_WhenExpiresWithinWeek_ReturnsDaysMessage(int daysUntilExpiry, string expected)
    {
        // Arrange
        var futureDate = DateTime.UtcNow.Date.AddDays(daysUntilExpiry);
        var model = new TestStockEntryDisplayModel(bestBeforeDate: futureDate);

        // Act
        var result = model.ExpiryDisplayText;

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ExpiryDisplayText_WhenExpiresMoreThanWeekAway_ReturnsFormattedDate()
    {
        // Arrange
        var futureDate = DateTime.UtcNow.Date.AddDays(14);
        var model = new TestStockEntryDisplayModel(bestBeforeDate: futureDate);

        // Act
        var result = model.ExpiryDisplayText;

        // Assert
        result.Should().StartWith("Expires");
        result.Should().Contain(futureDate.ToString("MMM d, yyyy"));
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_WithNoExpiryDate_ReturnsFalse()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: null);

        // Assert
        model.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithPastDate_ReturnsTrue()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: DateTime.UtcNow.Date.AddDays(-1));

        // Assert
        model.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WithTodayDate_ReturnsFalse()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: DateTime.UtcNow.Date);

        // Assert
        model.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WithFutureDate_ReturnsFalse()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: DateTime.UtcNow.Date.AddDays(5));

        // Assert
        model.IsExpired.Should().BeFalse();
    }

    #endregion

    #region DaysUntilExpiry Tests

    [Fact]
    public void DaysUntilExpiry_WithNoExpiryDate_ReturnsNull()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: null);

        // Assert
        model.DaysUntilExpiry.Should().BeNull();
    }

    [Fact]
    public void DaysUntilExpiry_WithExpiredDate_ReturnsNegative()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: DateTime.UtcNow.Date.AddDays(-3));

        // Assert
        model.DaysUntilExpiry.Should().Be(-3);
    }

    [Fact]
    public void DaysUntilExpiry_WithTodayDate_ReturnsZero()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: DateTime.UtcNow.Date);

        // Assert
        model.DaysUntilExpiry.Should().Be(0);
    }

    [Fact]
    public void DaysUntilExpiry_WithFutureDate_ReturnsPositive()
    {
        // Arrange
        var model = new TestStockEntryDisplayModel(bestBeforeDate: DateTime.UtcNow.Date.AddDays(10));

        // Assert
        model.DaysUntilExpiry.Should().Be(10);
    }

    #endregion

    #region FEFO Sorting Tests

    [Fact]
    public void FEFOSorting_NullExpiryDatesLast()
    {
        // Arrange
        var entries = new List<TestStockEntryDisplayModel>
        {
            new(bestBeforeDate: null, id: "no-expiry"),
            new(bestBeforeDate: DateTime.UtcNow.Date.AddDays(5), id: "expires-soon"),
            new(bestBeforeDate: DateTime.UtcNow.Date.AddDays(10), id: "expires-later"),
        };

        // Act - Sort using FEFO (nulls last, then by date ascending)
        var sorted = entries
            .OrderBy(e => e.BestBeforeDate == null ? 1 : 0)
            .ThenBy(e => e.BestBeforeDate)
            .ToList();

        // Assert
        sorted[0].Id.Should().Be("expires-soon");
        sorted[1].Id.Should().Be("expires-later");
        sorted[2].Id.Should().Be("no-expiry");
    }

    [Fact]
    public void FEFOSorting_ExpiredItemsFirst()
    {
        // Arrange
        var entries = new List<TestStockEntryDisplayModel>
        {
            new(bestBeforeDate: DateTime.UtcNow.Date.AddDays(5), id: "future"),
            new(bestBeforeDate: DateTime.UtcNow.Date.AddDays(-2), id: "expired"),
            new(bestBeforeDate: DateTime.UtcNow.Date, id: "today"),
        };

        // Act - Sort using FEFO
        var sorted = entries
            .OrderBy(e => e.BestBeforeDate == null ? 1 : 0)
            .ThenBy(e => e.BestBeforeDate)
            .ToList();

        // Assert
        sorted[0].Id.Should().Be("expired");
        sorted[1].Id.Should().Be("today");
        sorted[2].Id.Should().Be("future");
    }

    #endregion

    #region Test Helper Class

    /// <summary>
    /// Test implementation of the StockEntryDisplayModel logic.
    /// Mirrors the implementation in QuickConsumePage.xaml.cs
    /// </summary>
    private class TestStockEntryDisplayModel
    {
        public string Id { get; }
        public DateTime? BestBeforeDate { get; }

        public TestStockEntryDisplayModel(DateTime? bestBeforeDate, string id = "test")
        {
            Id = id;
            BestBeforeDate = bestBeforeDate;
        }

        public bool IsExpired => BestBeforeDate.HasValue && BestBeforeDate.Value.Date < DateTime.UtcNow.Date;

        public int? DaysUntilExpiry => BestBeforeDate.HasValue
            ? (int)(BestBeforeDate.Value.Date - DateTime.UtcNow.Date).TotalDays
            : null;

        public string ExpiryDisplayText
        {
            get
            {
                if (!BestBeforeDate.HasValue)
                    return "No expiry date";

                if (IsExpired)
                    return $"EXPIRED ({BestBeforeDate.Value:MMM d, yyyy})";

                if (DaysUntilExpiry <= 0)
                    return "Expires today";

                if (DaysUntilExpiry == 1)
                    return "Expires tomorrow";

                if (DaysUntilExpiry <= 7)
                    return $"Expires in {DaysUntilExpiry} days";

                return $"Expires {BestBeforeDate.Value:MMM d, yyyy}";
            }
        }
    }

    #endregion
}
