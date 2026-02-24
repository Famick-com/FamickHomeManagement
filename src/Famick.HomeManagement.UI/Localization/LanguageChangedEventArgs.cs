namespace Famick.HomeManagement.UI.Localization;

/// <summary>
/// Event arguments for language change events.
/// </summary>
public class LanguageChangedEventArgs : EventArgs
{
    public string OldLanguage { get; }
    public string NewLanguage { get; }

    public LanguageChangedEventArgs(string oldLanguage, string newLanguage)
    {
        OldLanguage = oldLanguage;
        NewLanguage = newLanguage;
    }
}
