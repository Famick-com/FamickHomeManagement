namespace Famick.HomeManagement.Shared.PhoneFormatting;

public static class CountryPhoneFormats
{
    public static readonly CountryPhoneFormat UnitedStates =
        new("US", "United States", "+1", "(000) 000-0000", 10, 10);

    public static readonly CountryPhoneFormat Canada =
        new("CA", "Canada", "+1", "000-000-0000", 10, 10);

    public static readonly CountryPhoneFormat UnitedKingdom =
        new("GB", "United Kingdom", "+44", null, 9, 11);

    public static readonly CountryPhoneFormat Ireland =
        new("IE", "Ireland", "+353", null, 7, 10);

    public static readonly CountryPhoneFormat Australia =
        new("AU", "Australia", "+61", "(00) 0000 0000", 10, 10);

    public static readonly CountryPhoneFormat NewZealand =
        new("NZ", "New Zealand", "+64", "(00) 000-0000", 9, 10);

    public static readonly CountryPhoneFormat Germany =
        new("DE", "Germany", "+49", null, 10, 12);

    public static readonly CountryPhoneFormat France =
        new("FR", "France", "+33", "00 00 00 00 00", 10, 10);

    public static readonly CountryPhoneFormat Spain =
        new("ES", "Spain", "+34", null, 9, 9);

    public static readonly CountryPhoneFormat Italy =
        new("IT", "Italy", "+39", null, 6, 11);

    public static readonly CountryPhoneFormat Netherlands =
        new("NL", "Netherlands", "+31", null, 9, 10);

    public static readonly CountryPhoneFormat Sweden =
        new("SE", "Sweden", "+46", null, 8, 10);

    public static readonly CountryPhoneFormat Norway =
        new("NO", "Norway", "+47", "00 00 00 00", 8, 8);

    public static readonly CountryPhoneFormat Denmark =
        new("DK", "Denmark", "+45", "00 00 00 00", 8, 8);

    public static readonly CountryPhoneFormat Finland =
        new("FI", "Finland", "+358", null, 7, 11);

    public static readonly CountryPhoneFormat Japan =
        new("JP", "Japan", "+81", "(000) 000-0000", 10, 11);

    public static readonly CountryPhoneFormat China =
        new("CN", "China", "+86", "(000) 0000-0000", 11, 11);

    public static readonly CountryPhoneFormat India =
        new("IN", "India", "+91", "000-000-0000", 10, 10);

    public static readonly CountryPhoneFormat Brazil =
        new("BR", "Brazil", "+55", "(00) 00000-0000", 10, 11);

    public static readonly CountryPhoneFormat Mexico =
        new("MX", "Mexico", "+52", "000 000 0000", 10, 10);

    public static readonly IReadOnlyList<CountryPhoneFormat> All = new[]
    {
        UnitedStates, Canada, UnitedKingdom, Ireland, Australia, NewZealand,
        Germany, France, Spain, Italy, Netherlands, Sweden, Norway, Denmark,
        Finland, Japan, China, India, Brazil, Mexico
    };

    public static CountryPhoneFormat Default => UnitedStates;

    public static CountryPhoneFormat? ByIso2(string? iso2) =>
        string.IsNullOrWhiteSpace(iso2)
            ? null
            : All.FirstOrDefault(c => string.Equals(c.Iso2, iso2, StringComparison.OrdinalIgnoreCase));

    public static CountryPhoneFormat? ByDialingCode(string? dialingCode)
    {
        if (string.IsNullOrWhiteSpace(dialingCode)) return null;
        if (string.Equals(dialingCode, "+1", StringComparison.Ordinal)) return UnitedStates;
        return All.FirstOrDefault(c => string.Equals(c.DialingCode, dialingCode, StringComparison.Ordinal));
    }
}
