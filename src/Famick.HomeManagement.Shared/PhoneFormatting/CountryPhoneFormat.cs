namespace Famick.HomeManagement.Shared.PhoneFormatting;

public sealed record CountryPhoneFormat(
    string Iso2,
    string Name,
    string DialingCode,
    string? Mask,
    int MinDigits,
    int MaxDigits)
{
    public bool HasFixedMask => !string.IsNullOrEmpty(Mask);

    public string Flag => Iso2Flag(Iso2);

    public string DisplayName => $"{Flag} {DialingCode} {Name}";

    public string ShortDisplay => $"{Flag} {DialingCode}";

    public string CodeAndName => $"{DialingCode} {Name}";

    public string CodeOnly => DialingCode;

    private static string Iso2Flag(string iso2)
    {
        if (string.IsNullOrEmpty(iso2) || iso2.Length != 2) return string.Empty;
        var upper = iso2.ToUpperInvariant();
        var first = char.ConvertFromUtf32(0x1F1E6 + (upper[0] - 'A'));
        var second = char.ConvertFromUtf32(0x1F1E6 + (upper[1] - 'A'));
        return first + second;
    }
}
