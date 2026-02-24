using Microsoft.Extensions.Localization;
using MudBlazor;

namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Custom MudLocalizer that uses our ILocalizationService for MudBlazor component translations.
/// </summary>
public class FamickMudLocalizer : MudLocalizer
{
    private readonly ILocalizationService _localizationService;

    public FamickMudLocalizer(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    public override LocalizedString this[string key]
    {
        get
        {
            // Look for MudBlazor keys in our translations under "mudblazor" namespace
            var translationKey = $"mudblazor.{key}";
            var value = _localizationService.GetString(translationKey);

            // If translation equals key (not found), return ResourceNotFound = true
            // This allows MudBlazor to fall back to built-in English
            if (value == translationKey)
            {
                return new LocalizedString(key, key, resourceNotFound: true);
            }

            return new LocalizedString(key, value);
        }
    }
}
