using System.Text.RegularExpressions;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// An ingredient line taken apart, such as "2 cups all-purpose flour, sifted".
/// </summary>
/// <param name="Amount">The amount, or the low end of a range ("2-3 cloves" is 2). Null when the line has none.</param>
/// <param name="Unit">The unit as written, lower-cased and without a trailing period ("cups", "clove"). Null when there is none.</param>
/// <param name="StandardUnit">The standard measure <paramref name="Unit"/> names, when it names one.</param>
/// <param name="Name">What the ingredient is, with sizes and preparation moved to the note ("flour").</param>
/// <param name="Note">Everything else: the text after a comma, in parentheses, and the words taken out of the name ("all-purpose, sifted").</param>
public sealed record ParsedIngredientLine(
    decimal? Amount,
    string? Unit,
    StandardUnit? StandardUnit,
    string Name,
    string? Note)
{
    /// <summary>
    /// The name with its sizes and preparation still in it ("all-purpose flour", "large eggs"), for a
    /// product named that way. Null when nothing was taken out of <see cref="Name"/>.
    /// </summary>
    public string? DescribedName { get; init; }

    /// <summary>
    /// <see cref="Note"/> without the words taken out of the name, for a line matched by
    /// <see cref="DescribedName"/>, whose product already says them.
    /// </summary>
    public string? NoteWithoutDescription { get; init; }
}

/// <summary>
/// Takes apart the ingredient lines of an imported recipe so each can be matched to a product and unit.
/// It only reads the line: deciding what product a name means is the importer's job, which matches
/// exactly, so the name here is cut down to what a product would be called.
/// </summary>
public static partial class IngredientLineParser
{
    private static readonly Dictionary<string, StandardUnit> StandardUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tsp"] = StandardUnit.Teaspoon, ["tsps"] = StandardUnit.Teaspoon,
        ["teaspoon"] = StandardUnit.Teaspoon, ["teaspoons"] = StandardUnit.Teaspoon,
        ["tbsp"] = StandardUnit.Tablespoon, ["tbsps"] = StandardUnit.Tablespoon, ["tbs"] = StandardUnit.Tablespoon,
        ["tbl"] = StandardUnit.Tablespoon, ["tblsp"] = StandardUnit.Tablespoon,
        ["tablespoon"] = StandardUnit.Tablespoon, ["tablespoons"] = StandardUnit.Tablespoon,
        ["c"] = StandardUnit.Cup, ["cup"] = StandardUnit.Cup, ["cups"] = StandardUnit.Cup,
        ["fl oz"] = StandardUnit.FluidOunce, ["fl. oz"] = StandardUnit.FluidOunce,
        ["fluid ounce"] = StandardUnit.FluidOunce, ["fluid ounces"] = StandardUnit.FluidOunce,
        ["pt"] = StandardUnit.Pint, ["pint"] = StandardUnit.Pint, ["pints"] = StandardUnit.Pint,
        ["qt"] = StandardUnit.Quart, ["quart"] = StandardUnit.Quart, ["quarts"] = StandardUnit.Quart,
        ["gal"] = StandardUnit.Gallon, ["gallon"] = StandardUnit.Gallon, ["gallons"] = StandardUnit.Gallon,
        ["ml"] = StandardUnit.Milliliter, ["milliliter"] = StandardUnit.Milliliter, ["milliliters"] = StandardUnit.Milliliter,
        ["millilitre"] = StandardUnit.Milliliter, ["millilitres"] = StandardUnit.Milliliter,
        ["l"] = StandardUnit.Liter, ["liter"] = StandardUnit.Liter, ["liters"] = StandardUnit.Liter,
        ["litre"] = StandardUnit.Liter, ["litres"] = StandardUnit.Liter,
        ["g"] = StandardUnit.Gram, ["gr"] = StandardUnit.Gram, ["gram"] = StandardUnit.Gram, ["grams"] = StandardUnit.Gram,
        ["kg"] = StandardUnit.Kilogram, ["kilo"] = StandardUnit.Kilogram, ["kilos"] = StandardUnit.Kilogram,
        ["kilogram"] = StandardUnit.Kilogram, ["kilograms"] = StandardUnit.Kilogram,
        ["oz"] = StandardUnit.Ounce, ["ounce"] = StandardUnit.Ounce, ["ounces"] = StandardUnit.Ounce,
        ["lb"] = StandardUnit.Pound, ["lbs"] = StandardUnit.Pound, ["pound"] = StandardUnit.Pound, ["pounds"] = StandardUnit.Pound
    };

    // Units with no standard measure, matched to a household unit by name.
    private static readonly HashSet<string> OtherUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "bag", "bags", "bottle", "bottles", "box", "boxes", "bunch", "bunches", "can", "cans", "clove", "cloves",
        "container", "containers", "dash", "dashes", "envelope", "envelopes", "handful", "handfuls", "head", "heads",
        "jar", "jars", "package", "packages", "packet", "packets", "pkg", "pinch", "pinches", "piece", "pieces",
        "slice", "slices", "splash", "splashes", "sprig", "sprigs", "stalk", "stalks", "stick", "sticks"
    };

    // Units that are an amount by themselves: "Pinch of salt" is one pinch.
    private static readonly HashSet<string> OneOfUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "dash", "handful", "pinch", "splash"
    };

    // Sizes and preparation, which describe the ingredient rather than name it: "2 large eggs, beaten" is
    // eggs. Words that change what the product is ("ground" beef, "whole" milk, "brown" sugar) stay.
    private static readonly HashSet<string> Descriptors = new(StringComparer.OrdinalIgnoreCase)
    {
        "all-purpose", "beaten", "boneless", "chilled", "chopped", "coarsely", "cold", "crumbled", "crushed",
        "cubed", "diced", "divided", "drained", "extra-large", "extra-virgin", "finely", "firmly", "fresh",
        "freshly", "granulated", "grated", "halved", "jumbo", "large", "lightly", "medium", "melted", "minced",
        "packed", "peeled", "rinsed", "ripe", "roughly", "shredded", "sifted", "skinless", "sliced", "small",
        "softened", "thinly", "trimmed", "unsalted", "very", "warm"
    };

    // A leading amount, optionally a range: "2", "1 1/2", "½", "2-3", "2 to 3"; or "a"/"an" for one. A number
    // must end where it ends, so "3/4" isn't read as 3 followed by "/4".
    private const string WholeNumber = $@"{QuantityText.Number}(?![0-9/⁄.])";

    [GeneratedRegex($@"^(?:(?<amount>{WholeNumber})(?:\s*(?:-|–|—|to)\s*{WholeNumber})?|(?<one>an?)(?=\s))\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmountRegex();

    // A unit right after the amount: two words ("fl oz", "fluid ounces") before one, "tbsp." with its period.
    [GeneratedRegex(@"^(?<unit>fl\.?\s*oz|fluid\s+ounces?|[\p{L}]+)\.?(?=\s|$)(?:\s+of(?=\s))?\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UnitRegex();

    [GeneratedRegex(@"\(([^()]*)\)")]
    private static partial Regex ParenthesesRegex();

    // What a line says about how much or whether to use it, at its end: "salt to taste", "parsley for garnish".
    [GeneratedRegex(@"\s+(?<note>(?:to taste|to serve|for (?:garnish|serving|dusting|greasing|topping|frying|drizzling)|as needed|optional|plus more\b.*|or more\b.*|at room temperature|room temperature))$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrailingNoteRegex();

    // Bullets and checkboxes some sites put before each line.
    [GeneratedRegex(@"^[\s\-–•*·▢☐□]+")]
    private static partial Regex BulletRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    public static ParsedIngredientLine Parse(string? line)
    {
        var text = WhitespaceRegex().Replace(line ?? string.Empty, " ").Trim();
        text = BulletRegex().Replace(text, string.Empty);

        var notes = new List<string>();

        // "1 (14.5 oz) can tomatoes", "butter (softened)": the parentheses are notes, wherever they are.
        text = ParenthesesRegex().Replace(text, m =>
        {
            AddNote(notes, m.Groups[1].Value);
            return " ";
        });
        text = WhitespaceRegex().Replace(text, " ").Trim();

        // "flour, sifted": after the first comma is how to prepare it.
        var comma = text.IndexOf(',');
        string? afterComma = null;
        if (comma >= 0)
        {
            afterComma = text[(comma + 1)..];
            text = text[..comma].Trim();
        }

        decimal? amount = null;
        var amountMatch = AmountRegex().Match(text);
        if (amountMatch.Success)
        {
            amount = amountMatch.Groups["amount"].Success
                ? ToAmount(QuantityText.Parse(amountMatch.Groups["amount"].Value))
                : 1m;
            text = text[amountMatch.Length..];
        }

        string? unit = null;
        StandardUnit? standardUnit = null;
        var unitMatch = UnitRegex().Match(text);
        if (unitMatch.Success
            && ReadUnit(unitMatch.Groups["unit"].Value) is { } found
            && (amount != null || OneOfUnits.Contains(Singular(found.Unit))))
        {
            (unit, standardUnit) = found;
            text = text[unitMatch.Length..];
            amount ??= 1m;
        }

        // "salt to taste", "butter or margarine".
        var trailing = TrailingNoteRegex().Match(text);
        if (trailing.Success)
        {
            AddNote(notes, trailing.Groups["note"].Value);
            text = text[..trailing.Index];
        }

        var or = text.IndexOf(" or ", StringComparison.OrdinalIgnoreCase);
        if (or > 0)
        {
            AddNote(notes, text[(or + 1)..]);
            text = text[..or];
        }

        // Descriptors leave the name in the order written, ahead of the comma's note: "all-purpose, sifted".
        var kept = new List<string>();
        var described = new List<string>();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Descriptors.Contains(word.Trim(',', '.', ';')))
                described.Add(word.Trim(',', '.', ';'));
            else
                kept.Add(word);
        }

        var name = CleanName(string.Join(' ', kept));
        var describedName = described.Count > 0 ? CleanName(text) : null;

        if (afterComma != null)
            AddNote(notes, afterComma);
        var undescribed = notes.Count > 0 ? string.Join(", ", notes) : null;
        if (described.Count > 0)
            notes.Insert(0, string.Join(' ', described));

        return new ParsedIngredientLine(amount, unit, standardUnit, name, notes.Count > 0 ? string.Join(", ", notes) : null)
        {
            DescribedName = describedName,
            NoteWithoutDescription = undescribed
        };
    }

    private static string CleanName(string name)
    {
        name = WhitespaceRegex().Replace(name, " ").Trim();
        if (name.StartsWith("of ", StringComparison.OrdinalIgnoreCase))
            name = name[3..];
        return name.Trim(' ', ',', '.', ';', ':', '-');
    }

    /// <summary>
    /// The names a product for <paramref name="name"/> could have: as written, then singular and plural,
    /// changing only the last word ("green onions" → "green onion"). Lower-cased, without repeats.
    /// </summary>
    public static IReadOnlyList<string> NameForms(string name)
    {
        var lower = name.Trim().ToLowerInvariant();
        if (lower.Length == 0)
            return [];

        var space = lower.LastIndexOf(' ');
        var head = lower[..(space + 1)];
        var last = lower[(space + 1)..];

        return new[] { lower, head + Singular(last), head + Plural(last) }
            .Where(f => f.Length > 0)
            .Distinct()
            .ToList();
    }

    private static (string Unit, StandardUnit? Standard)? ReadUnit(string written)
    {
        var unit = WhitespaceRegex().Replace(written, " ").Trim().TrimEnd('.');

        // "T" is a tablespoon and "t" a teaspoon; every other unit ignores case.
        if (unit == "T")
            return ("tbsp", StandardUnit.Tablespoon);
        if (unit == "t")
            return ("tsp", StandardUnit.Teaspoon);

        var key = unit.ToLowerInvariant().Replace("fl. oz", "fl oz").Replace("fl.oz", "fl oz").Replace("floz", "fl oz");
        if (StandardUnits.TryGetValue(key, out var standard))
            return (key, standard);
        if (OtherUnits.Contains(key))
            return (key, null);

        return null;
    }

    // Words that end in "s" without being plural.
    private static readonly HashSet<string> NotPlural = new(StringComparer.OrdinalIgnoreCase)
    {
        "molasses", "hummus", "couscous", "asparagus", "swiss", "grits", "oats", "lemongrass"
    };

    private static string Singular(string word)
    {
        if (NotPlural.Contains(word) || word.EndsWith("us") || word.EndsWith("ss"))
            return word;
        if (word.EndsWith("ies") && word.Length > 4)
            return word[..^3] + "y";
        if (word.EndsWith("oes") || word.EndsWith("ches") || word.EndsWith("shes") || word.EndsWith("xes"))
            return word[..^2];
        if (word.EndsWith('s') && word.Length > 3)
            return word[..^1];
        return word;
    }

    private static string Plural(string word)
    {
        if (word.EndsWith('s') || NotPlural.Contains(word))
            return word;
        if (word.EndsWith('y') && word.Length > 2 && !"aeiou".Contains(word[^2]))
            return word[..^1] + "ies";
        if (word.EndsWith("ch") || word.EndsWith("sh") || word.EndsWith('x') || word.EndsWith('o'))
            return word + "es";
        return word + "s";
    }

    // Recipe amounts are stored to four places; anything absurd is no amount at all.
    private static decimal? ToAmount(double value) =>
        value is > 0 and < 1_000_000 ? Math.Round((decimal)value, 4) : null;

    private static void AddNote(List<string> notes, string note)
    {
        note = note.Trim(' ', ',', ';');
        if (note.Length > 0)
            notes.Add(note);
    }
}
