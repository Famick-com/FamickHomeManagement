namespace Famick.HomeManagement.Shared.PhoneFormatting;

public static class PhoneNumberFormatter
{
    public readonly record struct ParseResult(CountryPhoneFormat Country, string LocalNumber);

    public static ParseResult Parse(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
        {
            return new ParseResult(CountryPhoneFormats.Default, string.Empty);
        }

        var trimmed = stored.Trim();

        if (!trimmed.StartsWith('+'))
        {
            return new ParseResult(CountryPhoneFormats.Default, trimmed);
        }

        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex > 0)
        {
            var dialingCode = trimmed[..spaceIndex];
            var local = trimmed[(spaceIndex + 1)..].TrimStart();
            var country = CountryPhoneFormats.ByDialingCode(dialingCode) ?? CountryPhoneFormats.Default;
            return new ParseResult(country, local);
        }

        for (var len = 4; len >= 2; len--)
        {
            if (trimmed.Length < len) continue;
            var candidate = trimmed[..len];
            if (!AllDigitsAfterPlus(candidate)) continue;
            var match = CountryPhoneFormats.ByDialingCode(candidate);
            if (match is not null)
            {
                return new ParseResult(match, trimmed[len..].TrimStart());
            }
        }

        return new ParseResult(CountryPhoneFormats.Default, trimmed);
    }

    private static bool AllDigitsAfterPlus(string candidate)
    {
        if (candidate.Length < 2 || candidate[0] != '+') return false;
        for (var i = 1; i < candidate.Length; i++)
        {
            if (!char.IsDigit(candidate[i])) return false;
        }
        return true;
    }

    public static string FormatForStorage(CountryPhoneFormat country, string? localNumber)
    {
        ArgumentNullException.ThrowIfNull(country);
        var local = (localNumber ?? string.Empty).Trim();
        return local.Length == 0 ? string.Empty : $"{country.DialingCode} {local}";
    }

    public static string FormatForDisplay(string? stored)
    {
        var parsed = Parse(stored);
        if (string.IsNullOrEmpty(parsed.LocalNumber)) return string.Empty;
        var formattedLocal = ApplyMask(parsed.Country, parsed.LocalNumber);
        return $"{parsed.Country.DialingCode} {formattedLocal}";
    }

    public static string ApplyMask(CountryPhoneFormat country, string? localNumber)
    {
        if (string.IsNullOrEmpty(localNumber)) return string.Empty;
        if (!country.HasFixedMask) return localNumber;

        var digits = OnlyDigits(localNumber);
        if (digits.Length == 0) return localNumber;

        var mask = country.Mask!;
        var result = new System.Text.StringBuilder(mask.Length + digits.Length);
        var digitIndex = 0;
        foreach (var maskChar in mask)
        {
            if (maskChar == '0')
            {
                if (digitIndex >= digits.Length) break;
                result.Append(digits[digitIndex++]);
            }
            else
            {
                result.Append(maskChar);
            }
        }
        if (digitIndex < digits.Length)
        {
            result.Append(digits, digitIndex, digits.Length - digitIndex);
        }
        return result.ToString();
    }

    private static string OnlyDigits(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var i = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch)) buffer[i++] = ch;
        }
        return new string(buffer[..i]);
    }

    public static int CountDigits(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        var count = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch)) count++;
        }
        return count;
    }

    public static string StripFormatting(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;
        Span<char> buffer = stackalloc char[stored.Length + 1];
        var i = 0;
        if (stored.StartsWith('+'))
        {
            buffer[i++] = '+';
        }
        foreach (var ch in stored)
        {
            if (char.IsDigit(ch)) buffer[i++] = ch;
        }
        return new string(buffer[..i]);
    }
}
