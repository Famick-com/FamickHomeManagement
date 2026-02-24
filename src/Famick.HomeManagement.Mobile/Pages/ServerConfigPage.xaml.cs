using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class ServerConfigPage : ContentPage
{
    private readonly ApiSettings _apiSettings;
    private readonly ShoppingApiClient _apiClient;

    public ServerConfigPage(ApiSettings apiSettings, ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiSettings = apiSettings;
        _apiClient = apiClient;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ServerUrlEntry.Text = _apiSettings.SelfHostedUrl;
    }

    private async void OnTestClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ServerUrlEntry.Text))
        {
            ShowStatus("Please enter a server URL", false);
            return;
        }

        TestButton.IsEnabled = false;
        ShowStatus("Testing connection...", null);

        try
        {
            var originalUrl = _apiSettings.SelfHostedUrl;
            _apiSettings.SelfHostedUrl = ServerUrlEntry.Text.Trim();
            _apiSettings.Mode = ServerMode.SelfHosted;

            var isHealthy = await _apiClient.CheckHealthAsync();

            if (!isHealthy)
            {
                _apiSettings.SelfHostedUrl = originalUrl;
            }

            ShowStatus(isHealthy ? "Connection successful!" : "Server not reachable", isHealthy);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", false);
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ServerUrlEntry.Text))
        {
            ShowStatus("Please enter a server URL", false);
            return;
        }

        _apiSettings.Mode = ServerMode.SelfHosted;
        _apiSettings.SelfHostedUrl = ServerUrlEntry.Text.Trim();
        await Shell.Current.GoToAsync("..");
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        _apiSettings.Reset();
        await Shell.Current.GoToAsync("..");
    }

    private void ShowStatus(string message, bool? success)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = true;
        StatusLabel.TextColor = success switch
        {
            true => Colors.Green,
            false => Colors.Red,
            null => Colors.Gray
        };
    }
}
