using System.Security.Cryptography;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// Generates human-readable, non-predictable short codes for storage bins.
/// Format: adjective-noun-number (e.g., "blue-oak-47")
/// </summary>
public static class ShortCodeGenerator
{
    // Word lists designed for readability and memorability (104 adjectives, 102 nouns)
    // Total combinations: 104 * 102 * 90 = 954,720 unique codes per tenant
    private static readonly string[] Adjectives =
    {
        // Colors
        "blue", "red", "green", "gold", "silver", "bronze", "amber", "coral",
        "ivory", "jade", "ruby", "pearl", "onyx", "azure", "crimson", "violet",

        // Qualities
        "swift", "calm", "bold", "warm", "cool", "bright", "dark", "soft",
        "loud", "quiet", "quick", "slow", "tall", "deep", "wide", "thin",
        "round", "flat", "sharp", "smooth", "fresh", "dry", "wet", "hot",
        "cold", "light", "heavy", "strong", "weak", "wild", "tame", "pure",
        "rich", "full", "open", "high", "low", "keen", "vast", "dense",

        // Nature-inspired
        "misty", "foggy", "sunny", "windy", "snowy", "rainy", "dusty", "sandy",
        "rocky", "mossy", "leafy", "woody", "grassy", "frosty", "dewy", "hazy",

        // Character
        "brave", "clever", "gentle", "humble", "noble", "proud", "loyal", "merry",
        "jolly", "witty", "zesty", "lively", "peaceful", "serene", "cosmic", "stellar",

        // Texture/Feel
        "silky", "rustic", "glossy", "matte", "velvet", "satin", "cozy", "crisp",
        "plush", "sleek", "polished", "rugged", "grainy", "woven", "braided", "knit"
    };

    private static readonly string[] Nouns =
    {
        // Trees
        "oak", "pine", "maple", "cedar", "birch", "elm", "ash", "willow",
        "spruce", "cherry", "walnut", "hickory", "cypress", "redwood", "sequoia", "palm",

        // Water/Weather
        "river", "lake", "ocean", "cloud", "rain", "snow", "wind", "storm",
        "creek", "brook", "pond", "bay", "cove", "tide", "wave", "mist",

        // Earth/Minerals
        "stone", "rock", "sand", "clay", "iron", "copper", "marble", "granite",
        "quartz", "flint", "slate", "basalt", "cobalt", "nickel", "zinc", "chrome",

        // Landforms
        "hill", "vale", "peak", "cliff", "cave", "field", "grove", "glen",
        "mesa", "ridge", "bluff", "knoll", "gorge", "canyon", "delta", "dune",

        // Animals
        "hawk", "eagle", "owl", "raven", "swan", "crane", "fox", "wolf",
        "bear", "deer", "hare", "mouse", "otter", "beaver", "badger", "lynx",

        // Plants/Flowers
        "fern", "moss", "vine", "reed", "sage", "mint", "basil", "thyme",
        "lotus", "lily", "rose", "tulip", "iris", "daisy", "orchid", "clover",

        // Celestial
        "star", "moon", "sun", "comet", "nova", "nebula"
    };

    /// <summary>
    /// Generates a random short code in the format: adjective-noun-number
    /// Uses cryptographically secure random number generation for unpredictability.
    /// </summary>
    /// <returns>A short code like "blue-oak-47"</returns>
    public static string Generate()
    {
        var adj = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        var num = RandomNumberGenerator.GetInt32(10, 100); // 10-99

        return $"{adj}-{noun}-{num}";
    }

    /// <summary>
    /// Calculates the total number of possible unique combinations.
    /// </summary>
    public static int TotalCombinations => Adjectives.Length * Nouns.Length * 90;
}
