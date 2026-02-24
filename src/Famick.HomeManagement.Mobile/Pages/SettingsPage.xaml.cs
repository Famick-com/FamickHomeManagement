using Famick.HomeManagement.Mobile.Pages.Wizard;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    private async void OnHomeSetupTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var onboardingService = services?.GetService<OnboardingService>();

        if (onboardingService != null)
        {
            Preferences.Default.Remove("home_setup_wizard_completed");
        }

        var wizardPage = services?.GetService<WizardHouseholdInfoPage>();
        if (wizardPage != null)
        {
            await Navigation.PushAsync(wizardPage);
        }
    }

    private async void OnNotificationsTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<NotificationSettingsPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnBarcodeScannerTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<BarcodeScannerSettingsPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnAboutTapped(object? sender, TappedEventArgs e)
    {
        var version = AppInfo.VersionString;
        var build = AppInfo.BuildString;

        var tenantName = "";
        try
        {
            var tenantStorage = Application.Current?.Handler?.MauiContext?.Services.GetService<TenantStorage>();
            if (tenantStorage != null)
            {
                tenantName = await tenantStorage.GetTenantNameAsync() ?? "";
            }
        }
        catch { }

        var aboutTitle = string.IsNullOrEmpty(tenantName) ? "About Famick Home" : $"About {tenantName}";

        await DisplayAlertAsync(
            aboutTitle,
            $"Version {version} (Build {build})\n\n" +
            "Famick Home Management\n" +
            "A companion app for managing your home." +
            (string.IsNullOrEmpty(tenantName) ? "" : $"\n\nHousehold: {tenantName}"),
            "OK");
    }
}
