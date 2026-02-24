using Famick.HomeManagement.Core.Interfaces.Plugins;
using FluentAssertions;

namespace Famick.HomeManagement.Shared.Tests.Unit;

/// <summary>
/// Tests for barcode variant generation functionality in ProductLookupPipelineContext.
/// Verifies that multi-format barcode storage works correctly for maximum scanning compatibility.
/// </summary>
public class BarcodeVariantTests
{
    #region GenerateBarcodeVariants Tests

    [Fact]
    public void GenerateBarcodeVariants_WithUpc12_ShouldReturnThreeVariants()
    {
        // Arrange - 12-digit UPC with check digit
        var barcode = "761720051108";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(3);
        variants.Should().Contain(v => v.Barcode == "0761720051108" && v.Format == "EAN-13");
        variants.Should().Contain(v => v.Barcode == "761720051108" && v.Format == "UPC-A");
        variants.Should().Contain(v => v.Barcode == "76172005110" && v.Format == "UPC-A-Core");
    }

    [Fact]
    public void GenerateBarcodeVariants_WithUpc11_ShouldReturnThreeVariants()
    {
        // Arrange - 11-digit UPC without check digit
        var barcode = "76172005110";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(3);
        variants.Should().Contain(v => v.Barcode == "0761720051108" && v.Format == "EAN-13");
        variants.Should().Contain(v => v.Barcode == "761720051108" && v.Format == "UPC-A");
        variants.Should().Contain(v => v.Barcode == "76172005110" && v.Format == "UPC-A-Core");
    }

    [Fact]
    public void GenerateBarcodeVariants_WithEan13StartingWith0_ShouldReturnThreeVariants()
    {
        // Arrange - 13-digit EAN starting with 0 (US product)
        var barcode = "0761720051108";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(3);
        variants.Should().Contain(v => v.Barcode == "0761720051108" && v.Format == "EAN-13");
        variants.Should().Contain(v => v.Barcode == "761720051108" && v.Format == "UPC-A");
        variants.Should().Contain(v => v.Barcode == "76172005110" && v.Format == "UPC-A-Core");
    }

    [Fact]
    public void GenerateBarcodeVariants_WithNonUsEan13_ShouldReturnEmpty()
    {
        // Arrange - 13-digit EAN NOT starting with 0 (non-US product)
        var barcode = "4006381333931"; // German product

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().BeEmpty();
    }

    [Fact]
    public void GenerateBarcodeVariants_WithEan8_ShouldReturnSingleVariant()
    {
        // Arrange - 8-digit EAN-8
        var barcode = "96385074";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(1);
        variants.First().Format.Should().Be("EAN-8");
    }

    [Fact]
    public void GenerateBarcodeVariants_WithGtin14_ShouldReturnSingleVariant()
    {
        // Arrange - 14-digit GTIN-14
        var barcode = "10614141999996";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(1);
        variants.First().Format.Should().Be("GTIN-14");
        variants.First().Barcode.Should().Be("10614141999996");
    }

    [Fact]
    public void GenerateBarcodeVariants_WithEmptyString_ShouldReturnEmpty()
    {
        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants("");

        // Assert
        variants.Should().BeEmpty();
    }

    [Fact]
    public void GenerateBarcodeVariants_WithNull_ShouldReturnEmpty()
    {
        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(null!);

        // Assert
        variants.Should().BeEmpty();
    }

    [Fact]
    public void GenerateBarcodeVariants_WithNonDigitCharacters_ShouldStripAndProcess()
    {
        // Arrange - barcode with dashes and spaces
        var barcode = "761-720-051108";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(3);
        variants.Should().Contain(v => v.Barcode == "761720051108" && v.Format == "UPC-A");
    }

    [Fact]
    public void GenerateBarcodeVariants_VariantNotesAreDescriptive()
    {
        // Arrange
        var barcode = "761720051108";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().Contain(v => v.Note.Contains("13 digits"));
        variants.Should().Contain(v => v.Note.Contains("12 digits"));
        variants.Should().Contain(v => v.Note.Contains("11 digits"));
    }

    [Fact]
    public void GenerateBarcodeVariants_DifferentInputFormats_ShouldProduceSameOutput()
    {
        // Arrange - same product in different formats
        var upc11 = "76172005110";
        var upc12 = "761720051108";
        var ean13 = "0761720051108";

        // Act
        var variants11 = ProductLookupPipelineContext.GenerateBarcodeVariants(upc11);
        var variants12 = ProductLookupPipelineContext.GenerateBarcodeVariants(upc12);
        var variants13 = ProductLookupPipelineContext.GenerateBarcodeVariants(ean13);

        // Assert - all should produce the same set of barcodes
        var barcodes11 = variants11.Select(v => v.Barcode).OrderBy(b => b).ToList();
        var barcodes12 = variants12.Select(v => v.Barcode).OrderBy(b => b).ToList();
        var barcodes13 = variants13.Select(v => v.Barcode).OrderBy(b => b).ToList();

        barcodes11.Should().BeEquivalentTo(barcodes12);
        barcodes12.Should().BeEquivalentTo(barcodes13);
    }

    #endregion

    #region CalculateCheckDigit Tests

    [Fact]
    public void CalculateCheckDigit_ForUpc11_ShouldReturnCorrectDigit()
    {
        // Arrange - known UPC: 761720051108 (check digit is 8)
        var core = "76172005110";

        // Act
        var checkDigit = ProductLookupPipelineContext.CalculateCheckDigit(core);

        // Assert
        checkDigit.Should().Be('8');
    }

    [Fact]
    public void CalculateCheckDigit_ForAnotherUpc_ShouldReturnCorrectDigit()
    {
        // Arrange - UPC 012345678905 (check digit is 5)
        var core = "01234567890";

        // Act
        var checkDigit = ProductLookupPipelineContext.CalculateCheckDigit(core);

        // Assert
        checkDigit.Should().Be('5');
    }

    [Fact]
    public void CalculateCheckDigit_ForEan7_ShouldWork()
    {
        // EAN-8: 96385074 (7 digits + check)
        var core = "9638507";

        // Act
        var checkDigit = ProductLookupPipelineContext.CalculateCheckDigit(core);

        // Assert
        checkDigit.Should().Be('4');
    }

    [Fact]
    public void CalculateCheckDigit_WithTooShortInput_ShouldThrow()
    {
        // Arrange
        var shortCore = "123456"; // Only 6 digits

        // Act & Assert
        var act = () => ProductLookupPipelineContext.CalculateCheckDigit(shortCore);
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region HasValidCheckDigit Tests

    [Fact]
    public void HasValidCheckDigit_WithValidUpc12_ShouldReturnTrue()
    {
        // Arrange
        var upc = "761720051108";

        // Act
        var result = ProductLookupPipelineContext.HasValidCheckDigit(upc, isEan: false);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasValidCheckDigit_WithInvalidUpc12_ShouldReturnFalse()
    {
        // Arrange - wrong check digit
        var upc = "761720051109";

        // Act
        var result = ProductLookupPipelineContext.HasValidCheckDigit(upc, isEan: false);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValidCheckDigit_WithValidEan13_ShouldReturnTrue()
    {
        // Arrange
        var ean = "0761720051108";

        // Act
        var result = ProductLookupPipelineContext.HasValidCheckDigit(ean, isEan: true);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasValidCheckDigit_WithValidEan8_ShouldReturnTrue()
    {
        // Arrange
        var ean8 = "96385074";

        // Act
        var result = ProductLookupPipelineContext.HasValidCheckDigit(ean8, isEan: true);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Real-World Barcode Tests

    [Theory]
    [InlineData("041303004579")] // Lactaid Milk
    [InlineData("028400097574")] // Doritos
    [InlineData("037600108577")] // Philadelphia Cream Cheese
    public void GenerateBarcodeVariants_WithRealUpcBarcodes_ShouldGenerateThreeVariants(string barcode)
    {
        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(barcode);

        // Assert
        variants.Should().HaveCount(3);
        variants.Should().Contain(v => v.Format == "EAN-13");
        variants.Should().Contain(v => v.Format == "UPC-A");
        variants.Should().Contain(v => v.Format == "UPC-A-Core");
    }

    [Fact]
    public void GenerateBarcodeVariants_KrogerPaddedFormat_ShouldGenerateVariants()
    {
        // Arrange - Kroger often stores as 13 digits with leading 0, no check digit
        // This is like EAN-13 format: 0076172005110 (padded, no check)
        var krogerBarcode = "0076172005110";

        // Act
        var variants = ProductLookupPipelineContext.GenerateBarcodeVariants(krogerBarcode);

        // Assert - should still work by extracting the 11-digit core
        variants.Should().NotBeEmpty();
    }

    #endregion
}
