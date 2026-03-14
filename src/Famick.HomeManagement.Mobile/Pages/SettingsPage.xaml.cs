using Famick.HomeManagement.Mobile.Pages.MealPlanner;
using Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;
using Famick.HomeManagement.Mobile.Pages.Settings;
using Famick.HomeManagement.Mobile.Pages.Stores;
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

    private async void OnGroceryCatalogTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<ProductOnboardingIntroPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnMealPlannerTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<MealPlannerSettingsPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
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

    private async void OnStorageLocationsTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<StorageLocationsPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnStoresTapped(object? sender, TappedEventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<StoresListPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }

    private async void OnAboutTapped(object? sender, TappedEventArgs e)
    {
        var version = typeof(SettingsPage).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? AppInfo.VersionString;
        // Strip source hash suffix if present (e.g. "1.0.0-beta25+abc123" → "1.0.0-beta25")
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0) version = version[..plusIndex];
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
