using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardVehiclesPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly OnboardingService _onboardingService;
    private List<VehicleSummaryDto> _vehicles = new();

    public WizardVehiclesPage(ShoppingApiClient apiClient, OnboardingService onboardingService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _onboardingService = onboardingService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadVehiclesAsync();
    }

    private async Task LoadVehiclesAsync()
    {
        SetLoading(true);
        try
        {
            var result = await _apiClient.GetVehiclesAsync();
            if (result.Success && result.Data != null)
            {
                _vehicles = result.Data;
                MainThread.BeginInvokeOnMainThread(() => RenderVehiclesList());
            }
        }
        catch { }
        finally
        {
            SetLoading(false);
        }
    }

    private void RenderVehiclesList()
    {
        VehiclesListLayout.Children.Clear();
        EmptyVehiclesLabel.IsVisible = _vehicles.Count == 0;

        foreach (var vehicle in _vehicles)
        {
            var border = new Border
            {
                Padding = new Thickness(15),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
                Margin = new Thickness(0, 4)
            };
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E0E0E0"), Color.FromArgb("#444444"));
            border.SetAppThemeColor(Border.BackgroundColorProperty, Colors.White, Color.FromArgb("#2A2A2A"));

            var grid = new Grid { ColumnSpacing = 10 };
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            // Info
            var infoStack = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center };
            var nameLabel = new Label { Text = vehicle.DisplayName, FontAttributes = FontAttributes.Bold };
            nameLabel.SetAppThemeColor(Label.TextColorProperty, Colors.Black, Colors.White);
            infoStack.Children.Add(nameLabel);

            var details = new List<string>();
            if (!string.IsNullOrEmpty(vehicle.Color)) details.Add(vehicle.Color);
            if (!string.IsNullOrEmpty(vehicle.LicensePlate)) details.Add(vehicle.LicensePlate);
            if (details.Count > 0)
            {
                var detailLabel = new Label { Text = string.Join(" | ", details), FontSize = 12 };
                detailLabel.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#666666"), Color.FromArgb("#999999"));
                infoStack.Children.Add(detailLabel);
            }
            Grid.SetColumn(infoStack, 0);

            // Edit button
            var editBtn = new Button
            {
                Text = "Edit", FontSize = 12, Padding = new Thickness(5),
                BackgroundColor = Colors.Transparent
            };
            editBtn.SetAppThemeColor(Button.TextColorProperty, Color.FromArgb("#1976D2"), Color.FromArgb("#90CAF9"));
            var capturedVehicle = vehicle;
            editBtn.Clicked += async (s, ev) =>
            {
                await Navigation.PushAsync(
                    new WizardVehicleEditPage(_apiClient, capturedVehicle, await GetHouseholdMembersAsync()));
            };
            Grid.SetColumn(editBtn, 1);

            // Delete button
            var deleteBtn = new Button
            {
                Text = "Delete", FontSize = 12, Padding = new Thickness(5),
                BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#E53935")
            };
            deleteBtn.Clicked += async (s, ev) =>
            {
                var confirm = await DisplayAlertAsync("Delete Vehicle",
                    $"Delete {capturedVehicle.DisplayName}?", "Delete", "Cancel");
                if (!confirm) return;

                var delResult = await _apiClient.DeleteVehicleAsync(capturedVehicle.Id);
                if (delResult.Success)
                    await LoadVehiclesAsync();
                else
                    ShowError(delResult.ErrorMessage ?? "Failed to delete vehicle.");
            };
            Grid.SetColumn(deleteBtn, 2);

            grid.Children.Add(infoStack);
            grid.Children.Add(editBtn);
            grid.Children.Add(deleteBtn);
            border.Content = grid;
            VehiclesListLayout.Children.Add(border);
        }
    }

    private async void OnAddVehicleClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            new WizardVehicleEditPage(_apiClient, null, await GetHouseholdMembersAsync()));
    }

    private async void OnCompleteClicked(object? sender, EventArgs e)
    {
        SetLoading(true);
        HideError();
        try
        {
            var result = await _apiClient.CompleteWizardAsync();
            if (result.Success)
            {
                _onboardingService.MarkHomeSetupWizardCompleted();

                // If running inside Shell (re-run from dashboard), pop back
                // If running as initial wizard (NavigationPage), transition to main app
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync("//DashboardPage");
                }
                else
                {
                    App.TransitionToMainApp();
                }
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Failed to complete setup.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"Error: {ex.Message}");
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async Task<List<HouseholdMemberDto>> GetHouseholdMembersAsync()
    {
        var result = await _apiClient.GetHouseholdMembersAsync();
        return result.Success && result.Data != null ? result.Data : new List<HouseholdMemberDto>();
    }

    private void SetLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorLabel.IsVisible = true;
        });
    }

    private void HideError()
    {
        MainThread.BeginInvokeOnMainThread(() => ErrorLabel.IsVisible = false);
    }
}
