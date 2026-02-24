using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class NotificationSettingsPage : ContentPage
{
    private List<NotificationPreferenceItemDto> _notifPrefs = new();

    public NotificationSettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadNotificationPreferencesAsync();
    }

    private async Task LoadNotificationPreferencesAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        PrefsContainer.IsVisible = false;

        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();
            if (apiClient == null) return;

            var result = await apiClient.GetNotificationPreferencesAsync();
            if (result.Success && result.Data != null)
            {
                _notifPrefs = result.Data;
                BuildPreferenceUI();
                PrefsContainer.IsVisible = true;
            }
        }
        catch
        {
            // Silently fail - preferences section just won't show items
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private void BuildPreferenceUI()
    {
        PrefsStack.Children.Clear();

        for (int i = 0; i < _notifPrefs.Count; i++)
        {
            var pref = _notifPrefs[i];

            var container = new VerticalStackLayout { Spacing = 4, Padding = new Thickness(16, 12) };

            var typeLabel = new Label
            {
                Text = pref.DisplayName,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            };
            typeLabel.SetAppThemeColor(Label.TextColorProperty,
                Color.FromArgb("#000000"), Color.FromArgb("#FFFFFF"));
            container.Children.Add(typeLabel);

            // Email toggle
            var emailRow = new HorizontalStackLayout { Spacing = 8 };
            var emailLabel = new Label { Text = "Email", FontSize = 13, VerticalOptions = LayoutOptions.Center };
            emailLabel.SetAppThemeColor(Label.TextColorProperty,
                Color.FromArgb("#666666"), Color.FromArgb("#999999"));
            var emailSwitch = new Switch
            {
                IsToggled = pref.EmailEnabled,
                OnColor = Color.FromArgb("#1976D2")
            };
            var capturedPref = pref;
            emailSwitch.Toggled += async (_, e) =>
            {
                capturedPref.EmailEnabled = e.Value;
                await SaveNotificationPreferencesAsync();
            };
            emailRow.Children.Add(emailLabel);
            emailRow.Children.Add(emailSwitch);
            container.Children.Add(emailRow);

            // Push toggle
            var pushRow = new HorizontalStackLayout { Spacing = 8 };
            var pushLabel = new Label { Text = "Push", FontSize = 13, VerticalOptions = LayoutOptions.Center };
            pushLabel.SetAppThemeColor(Label.TextColorProperty,
                Color.FromArgb("#666666"), Color.FromArgb("#999999"));
            var pushSwitch = new Switch
            {
                IsToggled = pref.PushEnabled,
                OnColor = Color.FromArgb("#1976D2")
            };
            pushSwitch.Toggled += async (_, e) =>
            {
                capturedPref.PushEnabled = e.Value;
                await SaveNotificationPreferencesAsync();
            };
            pushRow.Children.Add(pushLabel);
            pushRow.Children.Add(pushSwitch);
            container.Children.Add(pushRow);

            PrefsStack.Children.Add(container);

            // Separator between items (not after last)
            if (i < _notifPrefs.Count - 1)
            {
                var separator = new BoxView { HeightRequest = 1 };
                separator.SetAppThemeColor(BoxView.BackgroundColorProperty,
                    Color.FromArgb("#E8E8E8"), Color.FromArgb("#3A3A3A"));
                PrefsStack.Children.Add(separator);
            }
        }
    }

    private async Task SaveNotificationPreferencesAsync()
    {
        try
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();
            if (apiClient == null) return;

            await apiClient.UpdateNotificationPreferencesAsync(_notifPrefs);
        }
        catch
        {
            // Silently fail
        }
    }

    private async void OnViewAllNotificationsClicked(object? sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<NotificationsPage>();
        if (page != null)
        {
            await Navigation.PushAsync(page);
        }
    }
}
