using Famick.HomeManagement.Shared.Barcodes;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit.Barcodes;

public class WeightBarcodeParserTests
{
    #region IsType2Barcode Tests

    [Theory]
    [InlineData("212345012990", true)]   // Price-embedded (prefix 21)
    [InlineData("201234501234", true)]   // Price-embedded (prefix 20)
    [InlineData("281234500150", true)]   // Weight-embedded (prefix 28)
    [InlineData("291234500250", true)]   // Weight-embedded (prefix 29)
    [InlineData("0212345012990", true)]  // 13-digit EAN starting with 02
    [InlineData("312345012990", false)]  // First digit is not 2
    [InlineData("112345012990", false)]  // First digit is not 2
    [InlineData("761720051108", false)]  // Standard UPC, starts with 7
    [InlineData("2123450129", false)]    // Too short (10 digits)
    [InlineData("21234501299012", false)] // Too long (14 digits)
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("2ABCDE012990", false)]  // Non-numeric
    public void IsType2Barcode_ShouldDetectCorrectly(string? barcode, bool expected)
    {
        WeightBarcodeParser.IsType2Barcode(barcode).Should().Be(expected);
    }

    #endregion

    #region ParseType2Barcode Tests - Price Embedded

    [Fact]
    public void ParseType2Barcode_PriceEmbedded_Prefix20_ShouldExtractCorrectly()
    {
        // Layout: [2][0][1][2][3][4][0][1][2][9][9][check]
        // prefix = "20", item(1-5) = "01234", value(6-10) = "01299" = $12.99
        var barcode = BuildType2Barcode("20", "01234", "01299");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode);

        result.Should().NotBeNull();
        result!.ItemNumber.Should().Be("01234");
        result.Prefix.Should().Be("20");
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Price);
        result.EmbeddedValue.Should().Be(12.99m);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ParseType2Barcode_PriceEmbedded_Prefix25_ShouldExtractCorrectly()
    {
        // prefix = "25", item(1-5) = "56789", value = "00599" = $5.99
        var barcode = BuildType2Barcode("25", "56789", "00599");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode);

        result.Should().NotBeNull();
        result!.ItemNumber.Should().Be("56789");
        result.Prefix.Should().Be("25");
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Price);
        result.EmbeddedValue.Should().Be(5.99m);
    }

    [Fact]
    public void ParseType2Barcode_PriceEmbedded_ZeroPrice_ShouldReturnZero()
    {
        var barcode = BuildType2Barcode("20", "01234", "00000");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode);

        result.Should().NotBeNull();
        result!.EmbeddedValue.Should().Be(0m);
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Price);
    }

    #endregion

    #region ParseType2Barcode Tests - Weight Embedded

    [Fact]
    public void ParseType2Barcode_WeightEmbedded_Prefix28_ShouldExtractCorrectly()
    {
        // prefix = "28", item(1-5) = "81234", weight = 1.50 lbs
        var barcode = BuildType2Barcode("28", "81234", "00150");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode);

        result.Should().NotBeNull();
        result!.ItemNumber.Should().Be("81234");
        result.Prefix.Should().Be("28");
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Weight);
        result.EmbeddedValue.Should().Be(1.50m);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void ParseType2Barcode_WeightEmbedded_Prefix29_ShouldExtractCorrectly()
    {
        // prefix = "29", item(1-5) = "90010", weight = 2.50 lbs
        var barcode = BuildType2Barcode("29", "90010", "00250");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode);

        result.Should().NotBeNull();
        result!.Prefix.Should().Be("29");
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Weight);
        result.EmbeddedValue.Should().Be(2.50m);
    }

    #endregion

    #region ParseType2Barcode Tests - Alternate Item Number Position

    [Fact]
    public void ParseType2Barcode_AlternatePosition_ShouldExtractDigits2Through6()
    {
        // With itemNumberStart=2, item number is digits 2-6
        var barcode = BuildType2Barcode("20", "01234", "56789");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode, itemNumberStart: 2);

        result.Should().NotBeNull();
        // digits 2-6 of barcode "201234567890" = "12345"
        result!.ItemNumber.Should().Be("12345");
        result.Prefix.Should().Be("20");
    }

    [Fact]
    public void ParseType2Barcode_DefaultAndAlternate_ShouldGiveDifferentItemNumbers()
    {
        var barcode = BuildType2Barcode("21", "12345", "67890");

        var defaultResult = WeightBarcodeParser.ParseType2Barcode(barcode);
        var altResult = WeightBarcodeParser.ParseType2Barcode(barcode, 2);

        defaultResult.Should().NotBeNull();
        altResult.Should().NotBeNull();
        // Default (digits 1-5) = "12345" vs alternate (digits 2-6) = "23456"
        defaultResult!.ItemNumber.Should().Be("12345");
        altResult!.ItemNumber.Should().Be("23456");
    }

    #endregion

    #region ParseType2Barcode Tests - EAN-13 with 02 prefix

    [Fact]
    public void ParseType2Barcode_Ean13With02Prefix_ShouldStripLeadingZeroAndParse()
    {
        var upc12 = BuildType2Barcode("21", "12345", "01299");
        var ean13 = "0" + upc12;

        var result = WeightBarcodeParser.ParseType2Barcode(ean13);

        result.Should().NotBeNull();
        result!.Prefix.Should().Be("21");
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Price);
        result.EmbeddedValue.Should().Be(12.99m);
    }

    #endregion

    #region ParseType2Barcode Tests - Invalid inputs

    [Fact]
    public void ParseType2Barcode_NonType2Barcode_ShouldReturnNull()
    {
        WeightBarcodeParser.ParseType2Barcode("761720051108").Should().BeNull();
    }

    [Fact]
    public void ParseType2Barcode_NullOrEmpty_ShouldReturnNull()
    {
        WeightBarcodeParser.ParseType2Barcode(null).Should().BeNull();
        WeightBarcodeParser.ParseType2Barcode("").Should().BeNull();
    }

    [Fact]
    public void ParseType2Barcode_WrongLength_ShouldReturnNull()
    {
        WeightBarcodeParser.ParseType2Barcode("21234").Should().BeNull();
        WeightBarcodeParser.ParseType2Barcode("2123456789012345").Should().BeNull();
    }

    [Fact]
    public void ParseType2Barcode_InvalidItemNumberStart_ShouldReturnNull()
    {
        var barcode = BuildType2Barcode("20", "01234", "01299");
        WeightBarcodeParser.ParseType2Barcode(barcode, 0).Should().BeNull();
        WeightBarcodeParser.ParseType2Barcode(barcode, 3).Should().BeNull();
    }

    #endregion

    #region IsProducePlu Tests

    [Theory]
    [InlineData("4011", true)]    // Bananas
    [InlineData("94011", true)]   // Organic bananas (5-digit)
    [InlineData("3000", true)]    // 4-digit PLU
    [InlineData("99999", true)]   // Max 5-digit
    [InlineData("123", false)]    // Too short
    [InlineData("123456", false)] // Too long
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("401A", false)]   // Non-numeric
    public void IsProducePlu_ShouldDetectCorrectly(string? code, bool expected)
    {
        WeightBarcodeParser.IsProducePlu(code).Should().Be(expected);
    }

    #endregion

    #region Real-world barcode examples

    [Fact]
    public void ParseType2Barcode_RealWorldMeatBarcode_ShouldParse()
    {
        // Common meat department format: prefix 20, item 12345, price $8.49
        var barcode = BuildType2Barcode("20", "01234", "00849");

        var result = WeightBarcodeParser.ParseType2Barcode(barcode);

        result.Should().NotBeNull();
        result!.EmbeddedValue.Should().Be(8.49m);
        result.EmbeddingType.Should().Be(Type2EmbeddingType.Price);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Builds a valid 12-digit Type 2 UPC-A barcode with correct check digit.
    /// Layout: [prefix[0]] [item(5 digits at pos 1-5)] [value(5 digits at pos 6-10)] [check digit]
    /// IMPORTANT: item[0] must equal prefix[1] since position 1 is shared.
    /// </summary>
    private static string BuildType2Barcode(string prefix, string item, string value)
    {
        if (prefix.Length != 2 || item.Length != 5 || value.Length != 5)
            throw new ArgumentException("prefix=2 chars, item=5 chars, value=5 chars required");

        if (item[0] != prefix[1])
            throw new ArgumentException($"item[0] ('{item[0]}') must equal prefix[1] ('{prefix[1]}') since they share position 1");

        // Build 11-digit body: prefix[0] + item + value
        var body = prefix[0] + item + value;

        // Calculate UPC check digit
        var sum = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var digit = body[i] - '0';
            sum += (i % 2 == 0) ? digit * 3 : digit;
        }
        var remainder = sum % 10;
        var check = remainder == 0 ? 0 : 10 - remainder;

        return body + check;
    }

    #endregion
}
