namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Platform-specific storage for language preferences.
/// Web uses localStorage, MAUI uses Preferences.
/// Follows the ITokenStorage pattern.
/// </summary>
public interface ILanguagePreferenceStorage
{
    /// <summary>
    /// Gets the stored language preference.
    /// </summary>
    Task<string?> GetLanguagePreferenceAsync();

    /// <summary>
    /// Saves the language preference.
    /// </summary>
    Task SetLanguagePreferenceAsync(string languageCode);

    /// <summary>
    /// Clears the stored language preference.
    /// </summary>
    Task ClearLanguagePreferenceAsync();
}
