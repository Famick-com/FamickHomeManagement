using System.Text;
using FluentAssertions;
using Xunit;

namespace Famick.HomeManagement.Tests.Unit.Services;

/// <summary>
/// Tests for BLE scanner service pure logic.
/// Since the Mobile project (net10.0-android/ios) can't be referenced directly from a
/// net10.0 test project, these tests verify the algorithms inline.
/// The actual BleScannerService uses the same logic.
/// </summary>
public class BleScannerServiceTests
{
    #region ParseBarcodeFromBytes Tests

    [Fact]
    public void ParseBarcodeFromBytes_WithValidUtf8_ReturnsBarcode()
    {
        var data = Encoding.UTF8.GetBytes("0123456789012");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("0123456789012");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithCrLfTerminator_ReturnsTrimmedBarcode()
    {
        var data = Encoding.UTF8.GetBytes("0123456789012\r\n");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("0123456789012");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithCrTerminator_ReturnsTrimmedBarcode()
    {
        var data = Encoding.UTF8.GetBytes("0123456789012\r");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("0123456789012");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithLfTerminator_ReturnsTrimmedBarcode()
    {
        var data = Encoding.UTF8.GetBytes("0123456789012\n");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("0123456789012");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithNullTerminator_ReturnsTrimmedBarcode()
    {
        var data = Encoding.UTF8.GetBytes("0123456789012\0");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("0123456789012");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithMultipleTerminators_ReturnsTrimmedBarcode()
    {
        var data = Encoding.UTF8.GetBytes("0123456789012\r\n\0");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("0123456789012");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithEmptyArray_ReturnsNull()
    {
        var data = Array.Empty<byte>();

        var result = ParseBarcodeFromBytes(data);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithNullArray_ReturnsNull()
    {
        var result = ParseBarcodeFromBytes(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithWhitespaceOnly_ReturnsNull()
    {
        var data = Encoding.UTF8.GetBytes("   \r\n\0");

        var result = ParseBarcodeFromBytes(data);

        result.Should().BeNull();
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithUpcBarcode_ReturnsCorrectValue()
    {
        // UPC-A barcode
        var data = Encoding.UTF8.GetBytes("041570004173\r\n");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("041570004173");
    }

    [Fact]
    public void ParseBarcodeFromBytes_WithEanBarcode_ReturnsCorrectValue()
    {
        // EAN-13 barcode
        var data = Encoding.UTF8.GetBytes("4006381333931\n");

        var result = ParseBarcodeFromBytes(data);

        result.Should().Be("4006381333931");
    }

    #endregion

    #region ComputeHeuristicScore Tests

    [Fact]
    public void ComputeHeuristicScore_WithScannerKeyword_ReturnsPositiveScore()
    {
        var score = ComputeHeuristicScore("BLE Barcode Scanner", []);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ComputeHeuristicScore_WithManufacturerKeyword_ReturnsPositiveScore()
    {
        var score = ComputeHeuristicScore("Zebra CS4070", []);

        score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ComputeHeuristicScore_WithKnownServiceUuid_ReturnsHighScore()
    {
        // Nordic UART Service UUID - common for BLE scanners
        var nordicUartUuid = Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        var score = ComputeHeuristicScore("Unknown Device", [nordicUartUuid]);

        score.Should().BeGreaterOrEqualTo(40);
    }

    [Fact]
    public void ComputeHeuristicScore_WithHidServiceUuid_ReturnsScore()
    {
        var hidUuid = Guid.Parse("00001812-0000-1000-8000-00805f9b34fb");

        var score = ComputeHeuristicScore("Unknown Device", [hidUuid]);

        score.Should().BeGreaterOrEqualTo(25);
    }

    [Fact]
    public void ComputeHeuristicScore_WithUnknownDevice_ReturnsZero()
    {
        var randomUuid = Guid.NewGuid();

        var score = ComputeHeuristicScore("My Headphones", [randomUuid]);

        score.Should().Be(0);
    }

    [Fact]
    public void ComputeHeuristicScore_WithMultipleMatches_AccumulatesScore()
    {
        // "Socket Scanner" matches both manufacturer ("socket") and scanner keyword ("scanner")
        var nordicUartUuid = Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dcca9e");

        var score = ComputeHeuristicScore("Socket Scanner", [nordicUartUuid]);

        // socket (+20) + scanner (+30) + known UUID (+40) = 90
        score.Should().BeGreaterOrEqualTo(90);
    }

    [Fact]
    public void ComputeHeuristicScore_WithNullName_DoesNotThrow()
    {
        var score = ComputeHeuristicScore(null, []);

        score.Should().Be(0);
    }

    [Fact]
    public void ComputeHeuristicScore_IsCaseInsensitive()
    {
        var scoreLower = ComputeHeuristicScore("zebra scanner", []);
        var scoreUpper = ComputeHeuristicScore("ZEBRA SCANNER", []);
        var scoreMixed = ComputeHeuristicScore("Zebra Scanner", []);

        scoreLower.Should().Be(scoreUpper);
        scoreLower.Should().Be(scoreMixed);
    }

    [Fact]
    public void ComputeHeuristicScore_EmptyServiceUuids_DoesNotThrow()
    {
        var score = ComputeHeuristicScore("Some Device", []);

        score.Should().Be(0);
    }

    #endregion

    #region Logic Mirrors (same algorithms as BleScannerService)

    /// <summary>
    /// Mirror of BleScannerService.ParseBarcodeFromBytes for testing.
    /// </summary>
    private static string? ParseBarcodeFromBytes(byte[] data)
    {
        if (data == null || data.Length == 0) return null;

        var text = Encoding.UTF8.GetString(data).Trim('\r', '\n', '\0');
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    // Known scanner keywords (mirrors known_ble_scanners.json heuristicKeywords)
    private static readonly string[] ScannerKeywords = ["scanner", "barcode", "symbology", "hid", "scan"];
    private static readonly string[] ManufacturerKeywords = ["socket", "zebra", "honeywell", "tera", "netum", "inateck", "datalogic", "unitech"];

    private static readonly HashSet<Guid> KnownScannerServiceUuids =
    [
        Guid.Parse("6e400001-b5a3-f393-e0a9-e50e24dcca9e"),
        Guid.Parse("0000fff0-0000-1000-8000-00805f9b34fb"),
        Guid.Parse("00001800-0000-1000-8000-00805f9b34fb"),
        Guid.Parse("00001812-0000-1000-8000-00805f9b34fb")
    ];

    private static readonly Guid HidServiceUuid = Guid.Parse("00001812-0000-1000-8000-00805f9b34fb");

    /// <summary>
    /// Mirror of BleScannerService.ComputeHeuristicScore for testing.
    /// </summary>
    private static int ComputeHeuristicScore(string? deviceName, IReadOnlyList<Guid> serviceUuids)
    {
        var score = 0;
        var nameLower = deviceName?.ToLowerInvariant() ?? "";

        foreach (var keyword in ScannerKeywords)
        {
            if (nameLower.Contains(keyword, StringComparison.Ordinal))
                score += 30;
        }

        foreach (var keyword in ManufacturerKeywords)
        {
            if (nameLower.Contains(keyword, StringComparison.Ordinal))
                score += 20;
        }

        foreach (var uuid in serviceUuids)
        {
            if (KnownScannerServiceUuids.Contains(uuid))
                score += 40;
        }

        if (serviceUuids.Any(u => u == HidServiceUuid))
            score += 25;

        return score;
    }

    #endregion
}
