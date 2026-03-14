using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class UpgradePromptPopup : Popup
{
    public UpgradePromptPopup()
    {
        InitializeComponent();
    }

    public void Configure(string featureArea, string description, string requiredTier)
    {
        DescriptionLabel.Text = description;
        TierLabel.Text = $"Required plan: {requiredTier}";
    }

    private async void OnMaybeLaterClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
    }

    private async void OnUpgradeClicked(object? sender, EventArgs e)
    {
        await CloseAsync();

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
