namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Default ILocalizer implementation that delegates to ILocalizationService.
/// </summary>
public class Localizer : ILocalizer
{
    private readonly ILocalizationService _localizationService;

    public Localizer(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public string this[string key] => _localizationService.GetString(key);

    public string this[string key, params object[] arguments] =>
        _localizationService.GetString(key, arguments);

    public string Plural(string key, int count, params object[] arguments) =>
        _localizationService.GetPluralString(key, count, arguments);

    public string CurrentLanguage => _localizationService.CurrentLanguage;

    public IReadOnlyList<LanguageInfo> AvailableLanguages =>
        _localizationService.AvailableLanguages;
}
