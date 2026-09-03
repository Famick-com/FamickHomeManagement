using Famick.HomeManagement.Mobile.Pages.MealPlanner;
using Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;
using Famick.HomeManagement.Mobile.Pages.Settings;
using Famick.HomeManagement.Mobile.Pages.Stores;
using Famick.HomeManagement.Mobile.Pages.Wizard;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ApiSettings? _apiSettings;

    public SettingsPage()
    {
        InitializeComponent();
        _apiSettings = Application.Current?.Handler?.MauiContext?.Services
            .GetService<ApiSettings>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Phase 4 chunk 4.H — Connectivity toggle is only meaningful for
        // self-hosted households; cloud accounts always reach app.famick.com.
        if (_apiSettings is not null && _apiSettings.Mode == ServerMode.SelfHosted)
        {
            ConnectivitySection.IsVisible = true;
            UseProxyOnlySwitch.IsToggled = _apiSettings.UseProxyOnly;
        }
        else
        {
            ConnectivitySection.IsVisible = false;
        }
    }

    private void OnUseProxyOnlyToggled(object? sender, ToggledEventArgs e)
    {
        if (_apiSettings is null) return;
        _apiSettings.UseProxyOnly = e.Value;
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
        // Navigate to the Profile > Notifications tab
        await Shell.Current.GoToAsync("//NotificationSettingsPage");
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
        await Shell.Current.GoToAsync(nameof(AboutPage));
    }

    private async void OnResetAppTapped(object? sender, TappedEventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Reset app?",
            "This clears your sign-in, server configuration, and any cached sign-in lookups, then takes you back to the welcome page so you can choose a different sign-in method (email or QR code).\n\nYour data on the home server is not affected.",
            "Reset",
            "Cancel");
        if (!confirm) return;

        await AppReset.RunAsync();
        App.TransitionToOnboarding();
    }
}
