namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Information about an available language.
/// </summary>
/// <param name="Code">Language code (e.g., "en", "de", "fr")</param>
/// <param name="NativeName">Name in the language itself (e.g., "Deutsch")</param>
/// <param name="EnglishName">Name in English (e.g., "German")</param>
/// <param name="IsRtl">Whether the language is right-to-left</param>
public record LanguageInfo(
    string Code,
    string NativeName,
    string EnglishName,
    bool IsRtl = false
);
