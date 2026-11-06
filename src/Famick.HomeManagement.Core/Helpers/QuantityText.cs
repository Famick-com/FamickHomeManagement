using System.Globalization;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// Numbers as recipes write them: 30, 1.5, 1 1/2, 1-1/2, 1½, 1 ½, 1/2, 1⁄2 and ½. Shared by the
/// cooking-time and ingredient-line parsers.
/// </summary>
public static class QuantityText
{
    /// <summary>The single-character fractions a number can end with, or be.</summary>
    public const string Vulgar = "½¼¾⅓⅔⅛⅜⅝⅞⅕⅖⅗⅘⅙⅚";

    /// <summary>
    /// A regular-expression fragment matching one number. Only 0-9: \d would also take digits such as
    /// "١٥", which the invariant-culture parse rejects. Both the slash and the fraction slash (⁄) divide.
    /// </summary>
    public const string Number = $@"(?:[0-9]+(?:\.[0-9]+)?(?:(?:\s+|-)[0-9]+[/⁄][0-9]+|\s*[{Vulgar}])?|[0-9]+[/⁄][0-9]+|[{Vulgar}])";

    /// <summary>
    /// The value of text matched by <see cref="Number"/>. A zero denominator counts as nothing, so
    /// "1 1/0" is 1.
    /// </summary>
    public static double Parse(string number)
    {
        number = number.Trim().Replace('⁄', '/');
        double value = 0;

        var last = number[^1];
        if (Vulgar.Contains(last))
        {
            value += VulgarValue(last);
            number = number[..^1].Trim();
            if (number.Length == 0)
                return value;
        }

        var fraction = number.LastIndexOf('/');
        if (fraction < 0)
            return value + double.Parse(number, CultureInfo.InvariantCulture);

        // "1 1/2" or "1-1/2": the whole part, then the fraction.
        var split = number.LastIndexOfAny([' ', '\t', '-'], fraction);
        if (split >= 0)
        {
            value += double.Parse(number[..split].Trim(), CultureInfo.InvariantCulture);
            number = number[(split + 1)..];
            fraction = number.IndexOf('/');
        }

        var denominator = double.Parse(number[(fraction + 1)..], CultureInfo.InvariantCulture);
        return denominator == 0
            ? value
            : value + double.Parse(number[..fraction], CultureInfo.InvariantCulture) / denominator;
    }

    private static double VulgarValue(char fraction) => fraction switch
    {
        '½' => 1 / 2d,
        '¼' => 1 / 4d,
        '¾' => 3 / 4d,
        '⅓' => 1 / 3d,
        '⅔' => 2 / 3d,
        '⅛' => 1 / 8d,
        '⅜' => 3 / 8d,
        '⅝' => 5 / 8d,
        '⅞' => 7 / 8d,
        '⅕' => 1 / 5d,
        '⅖' => 2 / 5d,
        '⅗' => 3 / 5d,
        '⅘' => 4 / 5d,
        '⅙' => 1 / 6d,
        _ => 5 / 6d
    };
}
