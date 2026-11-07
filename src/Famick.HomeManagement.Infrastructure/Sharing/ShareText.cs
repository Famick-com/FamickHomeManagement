using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// Cleans what another household wrote before it is stored in this one: it is shown here as this
/// household's own data, so nothing in it may change how it looks or where a link goes.
/// </summary>
public static partial class ShareText
{
    public const int MaxHouseholdNameLength = 40;
    public const string UnnamedHousehold = "another household";

    /// <summary>
    /// The text without control or bidirectional-override characters (line breaks and tabs kept),
    /// within <paramref name="maxLength"/>; null stays null.
    /// </summary>
    public static string? Clean(string? text, int maxLength)
    {
        if (text == null)
            return null;

        var cleaned = new StringBuilder(Math.Min(text.Length, maxLength));
        foreach (var c in text)
        {
            if (c is '\n' or '\r' or '\t' || !IsHidden(c))
                cleaned.Append(c);
        }

        var result = cleaned.ToString();
        return result.Length <= maxLength ? result : result[..maxLength];
    }

    /// <summary><see cref="Clean"/>, for a field that can't be empty.</summary>
    public static string Required(string? text, int maxLength, string fallback) =>
        Clean(text, maxLength) is { Length: > 0 } cleaned && !string.IsNullOrWhiteSpace(cleaned) ? cleaned : fallback;

    /// <summary>
    /// The sharing household's name as it is shown in this one: on one line, short, and never a link,
    /// so a name can't pass for a message from Famick.
    /// </summary>
    public static string HouseholdName(string? name)
    {
        var single = WhitespaceRegex().Replace(Clean(name, 500) ?? string.Empty, " ").Trim();
        if (single.Length == 0 || LinkRegex().IsMatch(single))
            return UnnamedHousehold;
        return single.Length <= MaxHouseholdNameLength ? single : single[..MaxHouseholdNameLength].TrimEnd();
    }

    /// <summary>An absolute http or https address, or null: any other scheme is dropped, never followed.</summary>
    public static string? WebUrl(string? url, int maxLength = 2000)
    {
        var trimmed = url?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > maxLength
            || !Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return null;
        return trimmed;
    }

    /// <summary>
    /// A field that may be an address or plain words, as a recipe's source is ("Grandma's cookbook"):
    /// words are kept, an address only if it is http or https.
    /// </summary>
    public static string? TextOrWebUrl(string? value, int maxLength)
    {
        var cleaned = Clean(value, maxLength);
        if (string.IsNullOrWhiteSpace(cleaned))
            return cleaned;
        return Uri.TryCreate(cleaned.Trim(), UriKind.Absolute, out _) && !cleaned.Contains(' ')
            ? WebUrl(cleaned, maxLength)
            : cleaned;
    }

    // Control characters, and the bidirectional marks, embeddings, overrides and isolates that can make
    // text read differently from what it is. Other format characters stay: the zero-width joiner holds
    // emoji sequences together.
    private static bool IsHidden(char c) =>
        CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Control
        || c is '؜' or '‎' or '‏' or (>= '‪' and <= '‮') or (>= '⁦' and <= '⁩');

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"://|\bwww\.|\.(com|net|org|io|app|co|info|biz|xyz)\b", RegexOptions.IgnoreCase)]
    private static partial Regex LinkRegex();
}
