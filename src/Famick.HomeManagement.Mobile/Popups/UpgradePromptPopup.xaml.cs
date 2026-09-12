using CommunityToolkit.Maui.Views;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class UpgradePromptPopup : Popup
{
    private string? _requiredTier;

    public UpgradePromptPopup()
    {
        InitializeComponent();
    }

    public void Configure(string featureArea, string description, string requiredTier)
    {
        _requiredTier = requiredTier;
        DescriptionLabel.Text = description;
        TierLabel.Text = $"Required plan: {requiredTier}";
    }

    private async void OnMaybeLaterClicked(object? sender, EventArgs e)
    {
        await CloseAsync();
    }

    /// <summary>
    /// Sends the user to the plans screen rather than out to the web.
    /// </summary>
    /// <remarks>
    /// This used to open app.famick.com in the system browser, which was the only purchase
    /// path the app had. It is not the only one now, and leaving the app to buy something
    /// the app can sell is a worse experience than the one it was standing in for.
    ///
    /// <para>The web link still exists — it sits at the bottom of the plans screen, below
    /// the in-app options, which is a deliberate choice rather than a leftover.</para>
    ///
    /// <para><paramref name="featureArea"/> has been accepted by <see cref="Configure"/>
    /// and ignored since this popup was written; the required tier it implies is now passed
    /// along so the right plan is highlighted on arrival.</para>
    /// </remarks>
    private async void OnUpgradeClicked(object? sender, EventArgs e)
    {
        await CloseAsync();

        try
        {
            var route = nameof(Pages.Settings.PlansPage);

            if (!string.IsNullOrEmpty(_requiredTier))
            {
                route += $"?highlightTier={Uri.EscapeDataString(_requiredTier)}";
            }

            await Shell.Current.GoToAsync(route);
        }
        catch
        {
            // Navigation can fail if the shell is mid-transition. Nothing to recover: the
            // screen is also reachable from Settings.
        }
    }
}
