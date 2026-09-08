using Famick.HomeManagement.Core.Helpers;
using FluentAssertions;

namespace Famick.HomeManagement.Tests.Unit.Helpers;

public class ScannedBarcodeNormalizerTests
{
    [Theory]
    // Apple's Vision framework reports a US UPC-A as its 13-digit EAN-13 rendering.
    // These must collapse back to the 12-digit form that ProductBarcode rows store.
    [InlineData("0012345678905", "012345678905")]
    [InlineData("0036000291452", "036000291452")]
    [InlineData("0072655556270", "072655556270")]
    public void Collapses_the_ean13_rendering_of_a_us_upca(string scanned, string expected)
    {
        ScannedBarcodeNormalizer.Normalize(scanned).Should().Be(expected);
    }

    [Theory]
    // Already 12-digit (Android ML Kit) — normalization must be a no-op.
    [InlineData("012345678905")]
    [InlineData("036000291452")]
    public void Is_idempotent_for_values_already_in_upca_form(string scanned)
    {
        ScannedBarcodeNormalizer.Normalize(scanned).Should().Be(scanned);
    }

    [Theory]
    // Genuine EAN-13s that are not UPC-A renderings.
    [InlineData("5000112637922")]
    [InlineData("4006381333931")]
    // 13 digits, leading zero, but the check digit does not validate.
    [InlineData("0012345678901")]
    // Not 13 digits.
    [InlineData("96385074")]
    // Non-numeric payloads (storage-bin QR URLs, setup deep links).
    [InlineData("https://app.famick.com/storage/abc123/XY7Q")]
    [InlineData("famick://setup?url=https://home.example.com")]
    [InlineData("0012345ABC905")]
    public void Leaves_everything_else_untouched(string scanned)
    {
        ScannedBarcodeNormalizer.Normalize(scanned).Should().Be(scanned);
    }

    [Fact]
    public void Trims_surrounding_whitespace()
    {
        ScannedBarcodeNormalizer.Normalize("  0012345678905  ").Should().Be("012345678905");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Returns_empty_for_missing_input(string? scanned)
    {
        ScannedBarcodeNormalizer.Normalize(scanned).Should().BeEmpty();
    }
}
