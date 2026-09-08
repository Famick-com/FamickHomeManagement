using Famick.HomeManagement.Plugin.Abstractions;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// Normalizes barcode values coming off a camera scanner so that every platform
/// reports the same string for the same physical barcode.
/// </summary>
public static class ScannedBarcodeNormalizer
{
    /// <summary>
    /// Collapses the 13-digit EAN-13 rendering of a US UPC-A back to its 12-digit form.
    /// </summary>
    /// <remarks>
    /// Apple's Vision framework has no UPC-A symbology, so the native scanner maps
    /// UPC-A onto <c>VNBarcodeSymbology.Ean13</c> and iOS reports a US UPC-A as its
    /// 13-digit EAN-13 form with a leading zero. Android's ML Kit reports the 12-digit
    /// form, which is what stored <c>ProductBarcode</c> rows contain and what the
    /// server's exact-match barcode lookup compares against. Without this, the same
    /// grocery item scans successfully on Android and comes back "not found" on iOS.
    /// </remarks>
    /// <param name="scanned">The raw value reported by the scanner.</param>
    /// <returns>
    /// The 12-digit UPC-A when <paramref name="scanned"/> is its 13-digit rendering;
    /// otherwise the input unchanged.
    /// </returns>
    public static string Normalize(string? scanned)
    {
        if (string.IsNullOrWhiteSpace(scanned))
            return string.Empty;

        var value = scanned.Trim();

        // Only a 13-digit, all-numeric value with a leading zero can be a UPC-A in
        // EAN-13 clothing. Everything else (QR payloads, EAN-8, Code 128, ...) passes through.
        if (value.Length != 13 || value[0] != '0')
            return value;

        foreach (var c in value)
        {
            if (!char.IsAsciiDigit(c))
                return value;
        }

        var candidate = value[1..];

        // Only collapse when both renderings parse as the same UPC-A. A genuine EAN-13
        // that merely starts with a zero, or a zero-padded GTIN-14 fragment, projects to
        // different data and is left alone.
        if (!BarcodeParser.TryParse(value, out var asEan13) || asEan13 is not { Type: BarcodeType.UpcA })
            return value;

        if (!BarcodeParser.TryParse(candidate, out var asUpcA) || asUpcA is not { Type: BarcodeType.UpcA })
            return value;

        return asEan13.Data == asUpcA.Data ? candidate : value;
    }
}
