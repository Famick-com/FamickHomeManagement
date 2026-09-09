using Famick.HomeManagement.Shared.Barcodes;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Barcodes;

public class ShoppingListBarcodeMatcherTests
{
    /// <summary>Stand-in for the mobile app's cached list item.</summary>
    private sealed record Item(string Name, params string[] Barcodes);

    private static BarcodeMatch<Item>? Match(string? barcode, params Item[] items)
        => ShoppingListBarcodeMatcher.Match(barcode, items, i => i.Barcodes);

    #region Direct matching

    [Fact]
    public void Match_FindsItemByItsBarcode()
    {
        var milk = new Item("Milk", "761720051108");

        var match = Match("761720051108", new Item("Bread", "012345678905"), milk);

        match.Should().NotBeNull();
        match!.Item.Should().Be(milk);
        match.Kind.Should().Be(BarcodeMatchKind.Direct);
    }

    [Fact]
    public void Match_FindsItemByAnySecondaryBarcode()
    {
        // A product can carry several barcodes (different pack sizes, a store-brand reprint).
        var eggs = new Item("Eggs", "111111111116", "222222222226");

        Match("222222222226", eggs)!.Item.Should().Be(eggs);
    }

    [Fact]
    public void Match_IsCaseInsensitive()
    {
        var item = new Item("Widget", "abc123");

        Match("ABC123", item).Should().NotBeNull();
    }

    [Fact]
    public void Match_ReturnsNullWhenBarcodeIsNotOnTheList()
    {
        Match("999999999999", new Item("Bread", "012345678905")).Should().BeNull();
    }

    [Fact]
    public void Match_ReturnsNullForAnEmptyList()
    {
        Match("012345678905").Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Match_ReturnsNullForAnEmptyBarcode(string? barcode)
    {
        Match(barcode, new Item("Bread", "012345678905")).Should().BeNull();
    }

    [Fact]
    public void Match_SkipsItemsWithNoBarcodes()
    {
        // Items added by name have no linked product and therefore no barcodes.
        var byName = new Item("Something the shopper typed");
        var milk = new Item("Milk", "761720051108");

        Match("761720051108", byName, milk)!.Item.Should().Be(milk);
    }

    [Fact]
    public void Match_ReturnsTheFirstMatchInListOrder()
    {
        var first = new Item("First", "761720051108");
        var second = new Item("Second", "761720051108");

        Match("761720051108", first, second)!.Item.Should().Be(first);
    }

    #endregion

    #region Type 2 (variable weight/price) barcodes

    [Fact]
    public void Match_FallsBackToTheType2ItemNumber()
    {
        // The trailing digits of a Type 2 barcode encode the weight, so the raw string is
        // unique per package and can never equal a stored barcode. Only the item number can.
        var beef = new Item("Ground Beef", "81234");

        var match = Match("281234001500", beef);

        match.Should().NotBeNull();
        match!.Item.Should().Be(beef);
        match.Kind.Should().Be(BarcodeMatchKind.Type2ItemNumber);
    }

    [Fact]
    public void Match_CarriesTheEmbeddedWeight()
    {
        var match = Match("281234001500", new Item("Ground Beef", "81234"));

        match!.EmbeddedWeight.Should().Be(1.50m);
        match.EmbeddedPrice.Should().BeNull();
    }

    [Fact]
    public void Match_CarriesTheEmbeddedPrice()
    {
        var match = Match("212345012994", new Item("Deli Ham", "12345"));

        match!.EmbeddedPrice.Should().Be(12.99m);
        match.EmbeddedWeight.Should().BeNull();
    }

    [Fact]
    public void Match_CarriesTheEmbeddedValueEvenOnADirectHit()
    {
        // Guards the regression this change was written to avoid: an item whose stored barcode
        // happens to be the full Type 2 string still has to report its weight, or the purchase
        // records as a single unit and the shopper's price is silently dropped.
        var match = Match("281234001500", new Item("Ground Beef", "281234001500"));

        match!.Kind.Should().Be(BarcodeMatchKind.Direct);
        match.EmbeddedWeight.Should().Be(1.50m);
    }

    [Fact]
    public void Match_TriesTheAlternateItemNumberPosition()
    {
        // US standard puts the item number at digits 1-5; some stores use 2-6.
        var alternate = WeightBarcodeParser.ParseType2Barcode("281234001500", 2)!;
        var item = new Item("Store-specific", alternate.ItemNumber);

        var match = Match("281234001500", item);

        match.Should().NotBeNull();
        match!.Item.Should().Be(item);
        match.Kind.Should().Be(BarcodeMatchKind.Type2ItemNumber);
    }

    [Fact]
    public void Match_PrefersTheStandardItemNumberPositionOverTheAlternate()
    {
        var standard = WeightBarcodeParser.ParseType2Barcode("281234001500")!;
        var alternate = WeightBarcodeParser.ParseType2Barcode("281234001500", 2)!;
        alternate.ItemNumber.Should().NotBe(standard.ItemNumber, "the test needs the two to differ");

        var alternateItem = new Item("Alternate", alternate.ItemNumber);
        var standardItem = new Item("Standard", standard.ItemNumber);

        // Listed alternate-first so position, not list order, decides.
        Match("281234001500", alternateItem, standardItem)!.Item.Should().Be(standardItem);
    }

    [Fact]
    public void Match_ReturnsNullWhenNoItemNumberMatches()
    {
        Match("281234001500", new Item("Unrelated", "999999999999")).Should().BeNull();
    }

    [Fact]
    public void Match_DoesNotReportEmbeddedValuesForAnOrdinaryBarcode()
    {
        var match = Match("761720051108", new Item("Milk", "761720051108"));

        match!.EmbeddedPrice.Should().BeNull();
        match.EmbeddedWeight.Should().BeNull();
    }

    #endregion
}
