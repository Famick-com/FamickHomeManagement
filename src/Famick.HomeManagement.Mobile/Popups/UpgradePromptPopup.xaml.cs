using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class UpgradePromptPopup : Popup
{
    private readonly string _featureArea;

    public UpgradePromptPopup(string featureArea, string description, string requiredTier)
    {
        InitializeComponent();
        _featureArea = featureArea;
        DescriptionLabel.Text = description;
        TierLabel.Text = $"Required plan: {requiredTier}";
    }

    private void OnMaybeLaterClicked(object? sender, EventArgs e)
    {
        Close();
    }

    private async void OnUpgradeClicked(object? sender, EventArgs e)
    {
        Close();

        // Open billing page in browser (cloud only)
        try
        {
            await Browser.Default.OpenAsync("https://app.famick.com/settings/billing", BrowserLaunchMode.SystemPreferred);
        }
        catch
        {
            // Ignore if browser can't open
        }
    }
}
