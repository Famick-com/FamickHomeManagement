using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Core.Validators.Calendar;

/// <summary>
/// The RRULE subset Famick stores (issue #120): daily or slower, with only the parts that
/// pick days. BYHOUR, BYMINUTE and BYSECOND are refused because they turn a daily rule
/// into one that fires every minute. Applied on write and again before every expansion,
/// so a rule stored before this check cannot run either.
/// </summary>
public static partial class RecurrenceRuleCheck
{
    private static readonly HashSet<string> Frequencies = ["DAILY", "WEEKLY", "MONTHLY", "YEARLY"];

    /// <summary>
    /// Returns null when the rule is allowed, otherwise why it is not.
    /// </summary>
    public static string? Problem(string rule)
    {
        var seen = new HashSet<string>();
        foreach (var part in rule.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2 || pair[1].Length == 0)
                return $"'{part}' is not a NAME=value part";

            var name = pair[0].Trim().ToUpperInvariant();
            var value = pair[1].Trim().ToUpperInvariant();
            if (!seen.Add(name))
                return $"{name} appears more than once";

            var problem = name switch
            {
                "FREQ" => Frequencies.Contains(value) ? null : "FREQ must be DAILY, WEEKLY, MONTHLY or YEARLY",
                "INTERVAL" => IntBetween(value, 1, 1000) ? null : "INTERVAL must be between 1 and 1000",
                "COUNT" => IntBetween(value, 1, 10000) ? null : "COUNT must be between 1 and 10000",
                "UNTIL" => UntilPattern().IsMatch(value) ? null : "UNTIL must be a date or a UTC date-time",
                "BYDAY" => EachMatches(value, s => ByDayPattern().IsMatch(s)) ? null : "BYDAY has an invalid day",
                "BYMONTHDAY" => EachMatches(value, s => SignedBetween(s, 1, 31)) ? null : "BYMONTHDAY must be 1-31 or -31 to -1",
                "BYMONTH" => EachMatches(value, s => IntBetween(s, 1, 12)) ? null : "BYMONTH must be 1-12",
                "BYSETPOS" => EachMatches(value, s => SignedBetween(s, 1, 366)) ? null : "BYSETPOS must be 1-366 or -366 to -1",
                "WKST" => WeekdayPattern().IsMatch(value) ? null : "WKST must be a weekday",
                _ => $"{name} is not supported"
            };
            if (problem != null)
                return problem;
        }

        if (!seen.Contains("FREQ"))
            return "FREQ is required";
        if (seen.Contains("COUNT") && seen.Contains("UNTIL"))
            return "COUNT and UNTIL cannot both be set";
        return null;
    }

    private static bool IntBetween(string value, int min, int max) =>
        int.TryParse(value, System.Globalization.NumberStyles.None, null, out var n) && n >= min && n <= max;

    private static bool SignedBetween(string value, int min, int max) =>
        value.StartsWith('-') ? IntBetween(value[1..], min, max)
        : IntBetween(value.TrimStart('+'), min, max);

    private static bool EachMatches(string value, Func<string, bool> check) =>
        value.Split(',').All(check);

    [GeneratedRegex(@"^\d{8}(T\d{6}Z?)?$")]
    private static partial Regex UntilPattern();

    [GeneratedRegex(@"^([+-]?([1-9]|[1-4]\d|5[0-3]))?(MO|TU|WE|TH|FR|SA|SU)$")]
    private static partial Regex ByDayPattern();

    [GeneratedRegex(@"^(MO|TU|WE|TH|FR|SA|SU)$")]
    private static partial Regex WeekdayPattern();
}
