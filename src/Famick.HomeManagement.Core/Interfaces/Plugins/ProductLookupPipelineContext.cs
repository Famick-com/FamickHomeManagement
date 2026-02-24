using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Represents a barcode in a specific format with metadata.
/// </summary>
/// <param name="Barcode">The barcode value in this format</param>
/// <param name="Format">The format name (e.g., "EAN-13", "UPC-A")</param>
/// <param name="Note">Descriptive note for this format</param>
public record BarcodeVariant(string Barcode, string Format, string Note);

/// <summary>
/// Type of product lookup search
/// </summary>
public enum ProductLookupSearchType
{
    Barcode,
    Name
}

/// <summary>
/// Context passed between plugins in the lookup pipeline.
/// Contains accumulated results and search parameters.
/// </summary>
public class ProductLookupPipelineContext
{
    private static readonly Regex DigitsOnly = new(@"[^0-9]", RegexOptions.Compiled);
    /// <summary>
    /// The original search query (barcode or name)
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Type of search being performed
    /// </summary>
    public ProductLookupSearchType SearchType { get; }

    /// <summary>
    /// Maximum results requested
    /// </summary>
    public int MaxResults { get; }

    /// <summary>
    /// Accumulated results from previous plugins in the pipeline.
    /// Plugins can add new results or enrich existing ones.
    /// </summary>
    public List<ProductLookupResult> Results { get; }

    public ProductLookupPipelineContext(
        string query,
        ProductLookupSearchType searchType,
        int maxResults = 20)
    {
        Query = query;
        SearchType = searchType;
        MaxResults = maxResults;
        Results = new List<ProductLookupResult>();
    }

    /// <summary>
    /// Find an existing result that matches the given criteria.
    /// Matches by barcode (normalized) or by externalId+dataSource combination.
    /// </summary>
    public ProductLookupResult? FindMatchingResult(
        string? barcode = null,
        string? externalId = null,
        string? dataSource = null)
    {
        // Priority 1: Match by barcode (normalized for different formats)
        if (!string.IsNullOrEmpty(barcode))
        {
            var normalizedInput = NormalizeBarcode(barcode);
            var byBarcode = Results.FirstOrDefault(r =>
                !string.IsNullOrEmpty(r.Barcode) &&
                NormalizeBarcode(r.Barcode).Equals(normalizedInput, StringComparison.OrdinalIgnoreCase));
            if (byBarcode != null) return byBarcode;
        }

        // Priority 2: Match by externalId + dataSource (for same-source enrichment)
        if (!string.IsNullOrEmpty(externalId) && !string.IsNullOrEmpty(dataSource))
        {
            return Results.FirstOrDefault(r =>
                r.DataSources.TryGetValue(dataSource, out var id) &&
                id.Equals(externalId, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// Normalizes a barcode for comparison by stripping check digits and leading zeros.
    /// Handles UPC-A (12 digits), EAN-13 (13 digits), and various retailer formats.
    /// </summary>
    /// <remarks>
    /// Some systems (like Kroger) store barcodes without check digits and with extra padding.
    /// For example:
    /// - UPC-A with check: 761720051108 (12 digits)
    /// - Kroger format:    0076172005110 (13 digits, no check digit, padded)
    /// Both normalize to: 76172005110
    /// </remarks>
    public static string NormalizeBarcode(string barcode)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            return string.Empty;

        // Remove any non-digit characters
        var digits = DigitsOnly.Replace(barcode, "");
        if (string.IsNullOrEmpty(digits))
            return string.Empty;

        // Only strip check digit if it's actually a valid check digit
        // Some systems (like Kroger) store barcodes without check digits
        var withoutCheck = digits;

        if (digits.Length == 8 && HasValidCheckDigit(digits, isEan: true))
        {
            withoutCheck = digits[..7]; // EAN-8 without check
        }
        else if (digits.Length == 12 && HasValidCheckDigit(digits, isEan: false))
        {
            withoutCheck = digits[..11]; // UPC-A without check
        }
        else if (digits.Length == 13 && HasValidCheckDigit(digits, isEan: true))
        {
            withoutCheck = digits[..12]; // EAN-13 without check
        }
        else if (digits.Length == 14 && HasValidCheckDigit(digits, isEan: true))
        {
            withoutCheck = digits[..13]; // GTIN-14 without check
        }
        // For non-standard lengths or invalid check digits, keep as-is

        // Strip leading zeros for comparison
        var normalized = withoutCheck.TrimStart('0');

        // If all zeros, return "0"
        return string.IsNullOrEmpty(normalized) ? "0" : normalized;
    }

    /// <summary>
    /// Validates if the last digit of a barcode is a valid check digit.
    /// </summary>
    /// <param name="barcode">The barcode to validate</param>
    /// <param name="isEan">True for EAN/GTIN algorithm, false for UPC-A algorithm</param>
    /// <returns>True if the last digit is a valid check digit</returns>
    public static bool HasValidCheckDigit(string barcode, bool isEan)
    {
        if (barcode.Length < 2)
            return false;

        var checkDigit = barcode[^1] - '0';
        var data = barcode[..^1];
        var sum = 0;

        for (var i = 0; i < data.Length; i++)
        {
            var digit = data[i] - '0';
            if (isEan)
            {
                // EAN/GTIN: odd positions (0-indexed) × 1, even positions × 3
                sum += i % 2 == 0 ? digit : digit * 3;
            }
            else
            {
                // UPC-A: odd positions (0-indexed) × 3, even positions × 1
                sum += i % 2 == 0 ? digit * 3 : digit;
            }
        }

        var expectedCheck = (10 - (sum % 10)) % 10;
        return checkDigit == expectedCheck;
    }

    /// <summary>
    /// Calculates the UPC/EAN check digit for a given set of digits.
    /// Uses the standard GTIN check digit algorithm (also known as Luhn mod 10).
    /// </summary>
    /// <param name="digits">The barcode digits without check digit (11 digits for UPC, 12 for EAN-13)</param>
    /// <returns>The calculated check digit character</returns>
    public static char CalculateCheckDigit(string digits)
    {
        if (string.IsNullOrEmpty(digits) || digits.Length < 7)
            throw new ArgumentException("Digits must be at least 7 characters", nameof(digits));

        var sum = 0;
        // UPC/GTIN algorithm: positions (0-indexed) alternate × 3 and × 1
        // Position 0 × 3, Position 1 × 1, Position 2 × 3, etc.
        for (var i = 0; i < digits.Length; i++)
        {
            var digit = digits[i] - '0';
            sum += i % 2 == 0 ? digit * 3 : digit;
        }

        var checkDigit = (10 - (sum % 10)) % 10;
        return (char)('0' + checkDigit);
    }

    /// <summary>
    /// Extracts the 11-digit core from any UPC/EAN barcode format.
    /// This is the essential product identifier without check digits or leading padding.
    /// </summary>
    /// <param name="barcode">Input barcode (11-14 digits)</param>
    /// <returns>The 11-digit core, or null if extraction is not possible</returns>
    private static string? ExtractCore11Digits(string barcode)
    {
        if (string.IsNullOrEmpty(barcode))
            return null;

        var digits = DigitsOnly.Replace(barcode, "");
        if (digits.Length < 11 || digits.Length > 14)
            return null;

        switch (digits.Length)
        {
            case 11:
                // Already 11 digits - this is the core
                return digits;

            case 12:
                // UPC-A with check digit - validate and extract first 11
                if (HasValidCheckDigit(digits, isEan: false))
                {
                    return digits[..11];
                }
                // If no valid check digit, assume it's a padded 11-digit barcode
                // (some systems pad with leading zero)
                if (digits.StartsWith('0'))
                {
                    return digits[1..];
                }
                return null;

            case 13:
                // EAN-13 - check if it's a US product (starts with 0)
                if (!digits.StartsWith('0'))
                {
                    // Non-US EAN-13, no UPC equivalent
                    return null;
                }
                // US EAN-13 starting with 0
                if (HasValidCheckDigit(digits, isEan: true))
                {
                    // Has valid check digit - strip leading 0 and check digit
                    return digits[1..12];
                }
                // No valid check digit - might be Kroger format (padded, no check)
                // Strip leading 0 and last digit
                return digits[1..12];

            case 14:
                // GTIN-14 - not convertible to UPC
                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Generates all barcode format variants for maximum scanning compatibility.
    /// For US products (starting with 0 in EAN-13), generates:
    /// - EAN-13 (13 digits with check digit)
    /// - UPC-A with check digit (12 digits)
    /// - UPC-A without check digit (11 digits)
    /// </summary>
    /// <param name="barcode">Input barcode in any format (8-14 digits)</param>
    /// <returns>List of barcode variants with format metadata</returns>
    public static List<BarcodeVariant> GenerateBarcodeVariants(string barcode)
    {
        var variants = new List<BarcodeVariant>();

        if (string.IsNullOrWhiteSpace(barcode))
            return variants;

        var digits = DigitsOnly.Replace(barcode, "");
        if (string.IsNullOrEmpty(digits))
            return variants;

        // Handle EAN-8 (8 digits) - different system, no UPC conversion
        if (digits.Length == 8)
        {
            if (HasValidCheckDigit(digits, isEan: true))
            {
                variants.Add(new BarcodeVariant(digits, "EAN-8", "EAN-8 format (8 digits)"));
            }
            else
            {
                // 8 digits without valid check - add check digit
                var checkDigit = CalculateCheckDigit(digits[..7]);
                var ean8WithCheck = digits[..7] + checkDigit;
                variants.Add(new BarcodeVariant(ean8WithCheck, "EAN-8", "EAN-8 format (8 digits)"));
            }
            return variants;
        }

        // Handle GTIN-14 (14 digits) - package level, return as-is
        if (digits.Length == 14)
        {
            variants.Add(new BarcodeVariant(digits, "GTIN-14", "GTIN-14 format (14 digits)"));
            return variants;
        }

        // Handle non-US EAN-13 (13 digits NOT starting with 0)
        if (digits.Length == 13 && !digits.StartsWith('0'))
        {
            // Non-US product, no UPC equivalent exists
            // Return empty - we only generate variants for US products
            return variants;
        }

        // Extract the 11-digit core for US products (11-13 digit inputs starting with 0)
        var core11 = ExtractCore11Digits(digits);
        if (core11 == null)
            return variants;

        // Generate all three formats from the 11-digit core
        var upcCheckDigit = CalculateCheckDigit(core11);
        var upc12 = core11 + upcCheckDigit;
        var ean13 = "0" + upc12;

        // Verify EAN-13 check digit (should match since we calculated it correctly)
        // EAN-13 uses the same check digit as the underlying UPC-12
        variants.Add(new BarcodeVariant(ean13, "EAN-13", "EAN-13 format (13 digits)"));
        variants.Add(new BarcodeVariant(upc12, "UPC-A", "UPC-A with check digit (12 digits)"));
        variants.Add(new BarcodeVariant(core11, "UPC-A-Core", "UPC-A without check digit (11 digits)"));

        return variants;
    }

    /// <summary>
    /// Find all results that match the given barcode (using normalized comparison).
    /// </summary>
    public IEnumerable<ProductLookupResult> FindResultsByBarcode(string barcode)
    {
        if (string.IsNullOrEmpty(barcode)) yield break;

        var normalizedInput = NormalizeBarcode(barcode);
        foreach (var result in Results)
        {
            if (!string.IsNullOrEmpty(result.Barcode) &&
                NormalizeBarcode(result.Barcode).Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Add a new result to the pipeline.
    /// </summary>
    public void AddResult(ProductLookupResult result)
    {
        Results.Add(result);
    }

    /// <summary>
    /// Add multiple results to the pipeline.
    /// </summary>
    public void AddResults(IEnumerable<ProductLookupResult> results)
    {
        Results.AddRange(results);
    }
}
