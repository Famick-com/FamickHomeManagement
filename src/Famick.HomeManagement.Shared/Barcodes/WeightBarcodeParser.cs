namespace Famick.HomeManagement.Shared.Barcodes;

/// <summary>
/// Parsed information from a Type 2 UPC-A barcode (variable weight/price).
/// </summary>
public record Type2BarcodeInfo(
    string ItemNumber,
    string Prefix,
    Type2EmbeddingType EmbeddingType,
    decimal EmbeddedValue,
    char CheckDigit,
    bool IsValid);

/// <summary>
/// Whether the Type 2 barcode embeds a price or a weight.
/// </summary>
public enum Type2EmbeddingType
{
    /// <summary>Prefixes 20-27: value is a price in dollars ($$$cc / 100)</summary>
    Price,

    /// <summary>Prefixes 28-29: value is weight in pounds (in 0.01 lb increments)</summary>
    Weight
}

/// <summary>
/// Static utility for detecting and parsing Type 2 UPC-A barcodes (variable weight/price)
/// and produce PLU codes.
/// </summary>
public static class WeightBarcodeParser
{
    /// <summary>
    /// Returns true if the barcode is a Type 2 UPC-A (12 digits starting with '2')
    /// or a 13-digit EAN-13 starting with "02".
    /// </summary>
    public static bool IsType2Barcode(string? barcode)
    {
        if (string.IsNullOrEmpty(barcode))
            return false;

        // 13-digit EAN-13 starting with "02" is a Type 2 in EAN format
        if (barcode.Length == 13 && barcode[0] == '0' && barcode[1] == '2')
            return barcode.All(char.IsDigit);

        if (barcode.Length != 12)
            return false;

        if (barcode[0] != '2')
            return false;

        return barcode.All(char.IsDigit);
    }

    /// <summary>
    /// Parses a Type 2 UPC-A barcode, extracting the item number, embedded price/weight, and prefix.
    /// </summary>
    /// <param name="barcode">The 12-digit UPC-A barcode (or 13-digit EAN-13 starting with "02").</param>
    /// <param name="itemNumberStart">
    /// Starting digit position (0-indexed) for the 5-digit item number.
    /// Default: 1 (US standard: digits 1-5). Some stores use 2 (digits 2-6).
    /// </param>
    /// <returns>Parsed barcode info, or null if not a valid Type 2 barcode.</returns>
    public static Type2BarcodeInfo? ParseType2Barcode(string? barcode, int itemNumberStart = 1)
    {
        if (string.IsNullOrEmpty(barcode))
            return null;

        // Handle 13-digit EAN-13 starting with "02" by stripping leading '0'
        if (barcode.Length == 13 && barcode[0] == '0' && barcode[1] == '2')
            barcode = barcode[1..];

        if (barcode.Length != 12 || barcode[0] != '2' || !barcode.All(char.IsDigit))
            return null;

        // Validate itemNumberStart bounds: item number is 5 digits,
        // and we need room for at least check digit at position 11
        if (itemNumberStart < 1 || itemNumberStart > 2)
            return null;

        var prefix = barcode[..2]; // positions 0-1
        var subPrefix = barcode[1] - '0';

        // Determine embedding type based on prefix
        var embeddingType = subPrefix >= 8 ? Type2EmbeddingType.Weight : Type2EmbeddingType.Price;

        // Extract item number (5 digits starting at itemNumberStart)
        var itemNumber = barcode.Substring(itemNumberStart, 5);

        // Extract value digits (5 digits after the item number)
        var valueStart = itemNumberStart + 5;
        var valueDigits = barcode.Substring(valueStart, 5);
        var rawValue = int.Parse(valueDigits);
        var embeddedValue = rawValue / 100.0m;

        var checkDigit = barcode[11];
        var isValid = CalculateUpcCheckDigit(barcode[..11]) == checkDigit;

        return new Type2BarcodeInfo(
            ItemNumber: itemNumber,
            Prefix: prefix,
            EmbeddingType: embeddingType,
            EmbeddedValue: embeddedValue,
            CheckDigit: checkDigit,
            IsValid: isValid);
    }

    /// <summary>
    /// Returns true if the code is a produce PLU code (4-5 numeric digits).
    /// </summary>
    public static bool IsProducePlu(string? code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        return code.Length is 4 or 5 && code.All(char.IsDigit);
    }

    /// <summary>
    /// Calculates the UPC/EAN check digit for an 11-digit string.
    /// </summary>
    private static char CalculateUpcCheckDigit(string digits)
    {
        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[i] - '0';
            // Odd positions (0-indexed) get multiplied by 3, even by 1
            sum += (i % 2 == 0) ? digit * 3 : digit;
        }

        var remainder = sum % 10;
        var check = remainder == 0 ? 0 : 10 - remainder;
        return (char)('0' + check);
    }
}
