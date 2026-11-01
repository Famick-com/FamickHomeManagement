using System.Globalization;
using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// A cooking time found in recipe text, such as "30 min" in "Bake for 30 min".
/// </summary>
/// <param name="Index">Where the phrase starts in the text.</param>
/// <param name="Length">How long the phrase is, so a client can turn exactly that text into a timer link.</param>
/// <param name="Duration">How long the timer runs. For a range ("20-25 minutes") this is the upper bound.</param>
/// <param name="MinimumDuration">The lower bound of a range; null when the phrase is a single time.</param>
public sealed record CookingTime(int Index, int Length, TimeSpan Duration, TimeSpan? MinimumDuration = null);

/// <summary>
/// Finds the cooking times in a recipe step's instructions, so the clients can offer a timer for each.
/// Nothing is stored: the instructions are the source, and a step can have several times or none.
/// </summary>
/// <remarks>
/// A time unit must follow the number, so "350 degrees", "2 cups" and a bare "30" are not times.
/// Single-letter units ("30m", "1h15m") must touch the number, and seconds have no single-letter or
/// word form, so "1990s" and "a second egg" are not times either. Anything over 24 hours is left alone.
/// </remarks>
public static partial class CookingTimeParser
{
    private static readonly TimeSpan Longest = TimeSpan.FromHours(24);

    private const string Vulgar = "½¼¾⅓⅔";

    // 30, 1.5, 1 1/2, 1-1/2, 1½, 1/2, ½
    private const string Number = $@"(?:\d+(?:\.\d+)?(?:(?:\s+|-)\d+/\d+|\s*[{Vulgar}])?|\d+/\d+|[{Vulgar}])";

    // A unit ends the word: "15 mint leaves" is not 15 minutes.
    private const string EndOfWord = @"(?!\p{L})";

    // Not inside a word or another number.
    private const string StartOfWord = @"(?<![\p{L}\p{N}./])";

    // Between a number and its unit: "30 minutes", "30minutes", "a 30-minute rest".
    private const string Gap = @"(?:\s*|-)";

    private const string Hour = $@"(?:{Number}{Gap}(?:hours?|hrs?){EndOfWord}|{Number}h{EndOfWord}|(?:half\s+an?|an?)\s+hour{EndOfWord})";
    private const string Minute = $@"(?:{Number}{Gap}(?:minutes?|mins?){EndOfWord}|{Number}m{EndOfWord}|(?:half\s+a|a)\s+minute{EndOfWord})";
    private const string Second = $@"(?:{Number}{Gap}(?:seconds?|secs?){EndOfWord})";

    // "1 hour 15 minutes", "1 hour and 15 minutes", "1h15m"; larger units first.
    private const string Joiner = @"(?:\s*(?:and\s+)?)";
    private const string Time = $@"(?:{Hour}(?:{Joiner}{Minute})?(?:{Joiner}{Second})?|{Minute}(?:{Joiner}{Second})?|{Second})";

    private const string To = @"\s*(?:-|–|—|\bto\b)\s*";

    // "20-25 minutes" (the unit is the upper bound's), "45 minutes to 1 hour", or a single time.
    // The low end is tried last, so "1-1/2 hours" is an hour and a half rather than "1 to 1/2 hours".
    [GeneratedRegex($@"{StartOfWord}(?:(?<low>{Number}){To})??(?<time>{Time})(?:{To}(?<high>{Time}))?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CookingTimeRegex();

    // One number-and-unit within a matched time.
    [GeneratedRegex($@"(?<number>{Number}){Gap}(?<unit>hours?|hrs?|h|minutes?|mins?|m|seconds?|secs?){EndOfWord}|(?<word>half\s+an?|an?)\s+(?<unit>hour|minute)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PartRegex();

    /// <summary>Every cooking time in <paramref name="text"/>, in the order they appear.</summary>
    public static IReadOnlyList<CookingTime> FindAll(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var times = new List<CookingTime>();
        foreach (Match match in CookingTimeRegex().Matches(text))
        {
            var (duration, firstUnit) = ParseTime(match.Groups["time"].Value);

            TimeSpan? minimum = null;
            if (match.Groups["low"].Success)
                minimum = TimeSpan.FromSeconds(Math.Round(ParseNumber(match.Groups["low"].Value) * firstUnit));
            else if (match.Groups["high"].Success)
            {
                minimum = duration;
                duration = ParseTime(match.Groups["high"].Value).Duration;
            }

            // "25-20 minutes" still means 20 to 25.
            if (minimum > duration)
                (minimum, duration) = (duration, minimum.Value);
            if (minimum == duration)
                minimum = null;

            if (duration <= TimeSpan.Zero || duration > Longest || minimum <= TimeSpan.Zero)
                continue;

            times.Add(new CookingTime(match.Index, match.Length, duration, minimum));
        }

        return times;
    }

    /// <summary>A matched time's length, and its first part's unit in seconds (for a range's low end).</summary>
    private static (TimeSpan Duration, double FirstUnitSeconds) ParseTime(string time)
    {
        double seconds = 0, firstUnit = 0;
        foreach (Match part in PartRegex().Matches(time))
        {
            var unit = UnitSeconds(part.Groups["unit"].Value);
            var amount = part.Groups["word"].Success
                ? (part.Groups["word"].Value.StartsWith("half", StringComparison.OrdinalIgnoreCase) ? 0.5 : 1)
                : ParseNumber(part.Groups["number"].Value);

            seconds += amount * unit;
            if (firstUnit == 0)
                firstUnit = unit;
        }

        return (TimeSpan.FromSeconds(Math.Round(seconds)), firstUnit);
    }

    private static double UnitSeconds(string unit) => char.ToLowerInvariant(unit[0]) switch
    {
        'h' => 3600,
        'm' => 60,
        _ => 1
    };

    /// <summary>30, 1.5, 1 1/2, 1-1/2, 1½, 1/2 or ½.</summary>
    private static double ParseNumber(string number)
    {
        number = number.Trim();
        double value = 0;

        var last = number[^1];
        if (Vulgar.Contains(last))
        {
            value += last switch
            {
                '½' => 1 / 2d,
                '¼' => 1 / 4d,
                '¾' => 3 / 4d,
                '⅓' => 1 / 3d,
                _ => 2 / 3d
            };
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
}
