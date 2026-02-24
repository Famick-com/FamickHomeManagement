using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Core localization service that loads and manages translations from JSON files.
/// </summary>
public class LocalizationService : ILocalizationService
{
    private readonly HttpClient _httpClient;
    private readonly ILanguagePreferenceStorage _preferenceStorage;
    private readonly ILogger<LocalizationService> _logger;

    private Dictionary<string, string> _translations = new();
    private List<LanguageInfo> _availableLanguages = new();
    private string _currentLanguage = "en";
    private bool _isInitialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public string CurrentLanguage => _currentLanguage;
    public IReadOnlyList<LanguageInfo> AvailableLanguages => _availableLanguages;

    public event EventHandler<LanguageChangedEventArgs>? LanguageChanged;

    public LocalizationService(
        HttpClient httpClient,
        ILanguagePreferenceStorage preferenceStorage,
        ILogger<LocalizationService> logger)
    {
        _httpClient = httpClient;
        _preferenceStorage = preferenceStorage;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        await _initLock.WaitAsync();
        try
        {
            if (_isInitialized) return;

            // Load available languages
            await LoadAvailableLanguagesAsync();

            // Resolve preferred language
            var preferredLanguage = await ResolvePreferredLanguageAsync();
            await LoadLanguageAsync(preferredLanguage);

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize localization");
            // Fall back to empty translations (keys will be returned as-is)
            _currentLanguage = "en";
            _isInitialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public string GetString(string key)
    {
        if (_translations.TryGetValue(key, out var value))
        {
            return value;
        }
        return key; // Return key as fallback
    }

    public string GetString(string key, params object[] arguments)
    {
        var template = GetString(key);
        if (arguments.Length == 0)
        {
            return template;
        }

        try
        {
            // Replace named placeholders like {field}, {min}, {max}
            var result = template;
            for (var i = 0; i < arguments.Length; i++)
            {
                result = result.Replace($"{{{i}}}", arguments[i]?.ToString() ?? "");
            }
            return result;
        }
        catch
        {
            return template;
        }
    }

    public string GetPluralString(string key, int count, params object[] arguments)
    {
        // Look for plural forms: key.zero, key.one, key.other
        var pluralKey = count switch
        {
            0 => $"{key}.zero",
            1 => $"{key}.one",
            _ => $"{key}.other"
        };

        // Try specific form first, fall back to "other"
        var template = _translations.TryGetValue(pluralKey, out var value)
            ? value
            : _translations.TryGetValue($"{key}.other", out var otherValue)
                ? otherValue
                : key;

        // Replace {count} placeholder
        template = template.Replace("{count}", count.ToString());

        // Replace additional arguments
        if (arguments.Length > 0)
        {
            for (var i = 0; i < arguments.Length; i++)
            {
                template = template.Replace($"{{{i}}}", arguments[i]?.ToString() ?? "");
            }
        }

        return template;
    }

    public async Task SetLanguageAsync(string languageCode)
    {
        if (_currentLanguage == languageCode) return;

        var oldLanguage = _currentLanguage;
        await LoadLanguageAsync(languageCode);
        await _preferenceStorage.SetLanguagePreferenceAsync(languageCode);

        LanguageChanged?.Invoke(this, new LanguageChangedEventArgs(oldLanguage, languageCode));
    }

    private async Task LoadAvailableLanguagesAsync()
    {
        try
        {
            var languagesConfig = await _httpClient.GetFromJsonAsync<LanguagesConfig>(
                "_content/Famick.HomeManagement.UI/locales/languages.json");

            if (languagesConfig?.Languages != null)
            {
                _availableLanguages = languagesConfig.Languages
                    .Select(l => new LanguageInfo(l.Code, l.NativeName, l.EnglishName, l.IsRtl))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load available languages, using default");
            _availableLanguages = new List<LanguageInfo>
            {
                new("en", "English", "English", false)
            };
        }
    }

    private async Task<string> ResolvePreferredLanguageAsync()
    {
        // Check stored preference (localStorage/Preferences)
        var storedPreference = await _preferenceStorage.GetLanguagePreferenceAsync();
        if (!string.IsNullOrEmpty(storedPreference) &&
            _availableLanguages.Any(l => l.Code == storedPreference))
        {
            return storedPreference;
        }

        // Default to English
        return "en";
    }

    private async Task LoadLanguageAsync(string languageCode)
    {
        try
        {
            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var jsonPath = $"_content/Famick.HomeManagement.UI/locales/{languageCode}.json?v={cacheBuster}";
            var response = await _httpClient.GetStreamAsync(jsonPath);
            var doc = await JsonDocument.ParseAsync(response);

            _translations.Clear();
            FlattenJson(doc.RootElement, "", _translations);
            _currentLanguage = languageCode;

            _logger.LogDebug("Loaded {Count} translations for language {Language}",
                _translations.Count, languageCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load language {Language}, falling back to en", languageCode);
            if (languageCode != "en")
            {
                await LoadLanguageAsync("en");
            }
        }
    }

    private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string> result)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    // Skip metadata
                    if (property.Name.StartsWith("_")) continue;

                    var newPrefix = string.IsNullOrEmpty(prefix)
                        ? property.Name
                        : $"{prefix}.{property.Name}";
                    FlattenJson(property.Value, newPrefix, result);
                }
                break;

            case JsonValueKind.String:
                result[prefix] = element.GetString() ?? prefix;
                break;

            case JsonValueKind.Number:
                result[prefix] = element.ToString();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                result[prefix] = element.GetBoolean().ToString().ToLowerInvariant();
                break;
        }
    }
}

internal record LanguagesConfig(
    string DefaultLanguage,
    List<LanguageConfigItem> Languages
);

internal record LanguageConfigItem(
    string Code,
    string NativeName,
    string EnglishName,
    bool IsRtl = false
);
