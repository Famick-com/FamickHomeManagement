using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Pages;

/// <summary>
/// Tests for stock spoil request formation logic.
/// Recreates QuickConsumeRequest to avoid MAUI project dependency.
/// </summary>
public class StockSpoilRequestTests
{
    private class TestQuickConsumeRequest
    {
        public Guid ProductId { get; set; }
        public decimal Amount { get; set; } = 1;
        public bool ConsumeAll { get; set; }
        public bool Spoiled { get; set; }
    }

    /// <summary>
    /// Mirrors the spoil request creation from OnSpoilClicked
    /// </summary>
    private static TestQuickConsumeRequest CreateSpoilRequest(Guid productId)
    {
        return new TestQuickConsumeRequest
        {
            ProductId = productId,
            ConsumeAll = true,
            Spoiled = true
        };
    }

    /// <summary>
    /// Mirrors the consume request creation from OnConsumeOneClicked
    /// </summary>
    private static TestQuickConsumeRequest CreateConsumeRequest(Guid productId)
    {
        return new TestQuickConsumeRequest
        {
            ProductId = productId,
            Amount = 1
        };
    }

    [Fact]
    public void SpoilRequest_SetsSpoiledTrue()
    {
        var request = CreateSpoilRequest(Guid.NewGuid());
        request.Spoiled.Should().BeTrue();
    }

    [Fact]
    public void SpoilRequest_SetsConsumeAllTrue()
    {
        var request = CreateSpoilRequest(Guid.NewGuid());
        request.ConsumeAll.Should().BeTrue();
    }

    [Fact]
    public void SpoilRequest_HasCorrectProductId()
    {
        var productId = Guid.NewGuid();
        var request = CreateSpoilRequest(productId);
        request.ProductId.Should().Be(productId);
    }

    [Fact]
    public void ConsumeRequest_SpoiledIsFalse()
    {
        var request = CreateConsumeRequest(Guid.NewGuid());
        request.Spoiled.Should().BeFalse();
    }

    [Fact]
    public void ConsumeRequest_ConsumeAllIsFalse()
    {
        var request = CreateConsumeRequest(Guid.NewGuid());
        request.ConsumeAll.Should().BeFalse();
    }

    [Fact]
    public void ConsumeRequest_AmountIsOne()
    {
        var request = CreateConsumeRequest(Guid.NewGuid());
        request.Amount.Should().Be(1);
    }
}
