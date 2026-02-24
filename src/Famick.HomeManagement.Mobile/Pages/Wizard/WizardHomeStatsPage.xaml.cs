using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardHomeStatsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    public WizardHomeStatsPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExistingDataAsync();
    }

    private async Task LoadExistingDataAsync()
    {
        SetLoading(true);
        try
        {
            var result = await _apiClient.GetWizardStateAsync();
            if (result.Success && result.Data != null)
            {
                var stats = result.Data.HomeStatistics;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    UnitEntry.Text = stats.Unit;
                    YearBuiltEntry.Text = stats.YearBuilt?.ToString();
                    SquareFootageEntry.Text = stats.SquareFootage?.ToString();

                    if (stats.Bedrooms.HasValue)
                    {
                        var bedStr = stats.Bedrooms >= 6 ? "6+" : stats.Bedrooms.ToString();
                        var bedIdx = BedroomsPicker.ItemsSource?.IndexOf(bedStr) ?? -1;
                        if (bedIdx >= 0) BedroomsPicker.SelectedIndex = bedIdx;
                    }

                    if (stats.Bathrooms.HasValue)
                    {
                        var bathStr = stats.Bathrooms >= 4 ? "4+" : stats.Bathrooms.Value.ToString("0.#");
                        var bathIdx = BathroomsPicker.ItemsSource?.IndexOf(bathStr) ?? -1;
                        if (bathIdx >= 0) BathroomsPicker.SelectedIndex = bathIdx;
                    }

                    HoaNameEntry.Text = stats.HoaName;
                    HoaContactEntry.Text = stats.HoaContactInfo;
                    HoaRulesLinkEntry.Text = stats.HoaRulesLink;
                });
            }
        }
        catch { }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardMaintenancePage>());
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        SetLoading(true);
        HideError();
        try
        {
            int? bedrooms = null;
            if (BedroomsPicker.SelectedItem is string bedStr)
            {
                bedrooms = bedStr == "6+" ? 6 : int.TryParse(bedStr, out var b) ? b : null;
            }

            decimal? bathrooms = null;
            if (BathroomsPicker.SelectedItem is string bathStr)
            {
                bathrooms = bathStr == "4+" ? 4m : decimal.TryParse(bathStr, out var ba) ? ba : null;
            }

            var dto = new HomeStatisticsDto
            {
                Unit = UnitEntry.Text?.Trim(),
                YearBuilt = int.TryParse(YearBuiltEntry.Text, out var yb) ? yb : null,
                SquareFootage = int.TryParse(SquareFootageEntry.Text, out var sf) ? sf : null,
                Bedrooms = bedrooms,
                Bathrooms = bathrooms,
                HoaName = HoaNameEntry.Text?.Trim(),
                HoaContactInfo = HoaContactEntry.Text?.Trim(),
                HoaRulesLink = HoaRulesLinkEntry.Text?.Trim()
            };

            var result = await _apiClient.SaveHomeStatisticsAsync(dto);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to save.");
                return;
            }

            await Navigation.PushAsync(
                Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardMaintenancePage>());
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
