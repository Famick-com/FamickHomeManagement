namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Primary interface for accessing localized strings in Razor components.
/// Inject this service to get localized text: @inject ILocalizer L
/// </summary>
public interface ILocalizer
{
    /// <summary>
    /// Gets a localized string for the given key.
    /// </summary>
    string this[string key] { get; }

    /// <summary>
    /// Gets a localized string with format arguments.
    /// </summary>
    string this[string key, params object[] arguments] { get; }

    /// <summary>
    /// Gets a localized string with plural form support.
    /// </summary>
    /// <param name="key">The translation key (e.g., "plurals.items")</param>
    /// <param name="count">The count for plural form selection</param>
    /// <param name="arguments">Optional format arguments</param>
    string Plural(string key, int count, params object[] arguments);

    /// <summary>
    /// Gets the current language code (e.g., "en", "de", "fr").
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Gets available languages.
    /// </summary>
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }
}
