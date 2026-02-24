namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Service for managing localization state and preferences.
/// Handles language resolution, loading translations, and persisting preferences.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Gets the current language code.
    /// </summary>
    string CurrentLanguage { get; }

    /// <summary>
    /// Gets a localized string for the given key.
    /// </summary>
    string GetString(string key);

    /// <summary>
    /// Gets a localized string with format arguments.
    /// </summary>
    string GetString(string key, params object[] arguments);

    /// <summary>
    /// Gets a localized string with plural form support.
    /// </summary>
    string GetPluralString(string key, int count, params object[] arguments);

    /// <summary>
    /// Changes the current language.
    /// </summary>
    Task SetLanguageAsync(string languageCode);

    /// <summary>
    /// Loads translations from JSON files. Must be called before using other methods.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Gets available languages.
    /// </summary>
    IReadOnlyList<LanguageInfo> AvailableLanguages { get; }

    /// <summary>
    /// Event raised when language changes.
    /// </summary>
    event EventHandler<LanguageChangedEventArgs>? LanguageChanged;
}
