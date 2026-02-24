using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Wizard;

public partial class WizardMaintenancePage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    public WizardMaintenancePage(ShoppingApiClient apiClient)
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
                var m = result.Data.MaintenanceItems;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    AcFilterSizesEntry.Text = m.AcFilterSizes;
                    SelectPickerItem(HeatingTypePicker, m.HeatingType);
                    SelectPickerItem(AcTypePicker, m.AcType);
                    FridgeFilterEntry.Text = m.FridgeWaterFilterType;
                    UnderSinkFilterEntry.Text = m.UnderSinkFilterType;
                    WholeHouseFilterEntry.Text = m.WholeHouseFilterType;
                    SelectPickerItem(WaterHeaterTypePicker, m.WaterHeaterType);
                    WaterHeaterSizeEntry.Text = m.WaterHeaterSize;
                    SelectPickerItem(BatteryTypePicker, m.SmokeCoDetectorBatteryType);
                });
            }
        }
        catch { }
        finally
        {
            SetLoading(false);
        }
    }

    private static void SelectPickerItem(Picker picker, string? value)
    {
        if (string.IsNullOrEmpty(value) || picker.ItemsSource == null) return;
        var idx = picker.ItemsSource.IndexOf(value);
        if (idx >= 0) picker.SelectedIndex = idx;
    }

    private async void OnSkipClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(
            Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardVehiclesPage>());
    }

    private async void OnNextClicked(object? sender, EventArgs e)
    {
        SetLoading(true);
        HideError();
        try
        {
            var dto = new MaintenanceItemsDto
            {
                AcFilterSizes = AcFilterSizesEntry.Text?.Trim(),
                HeatingType = HeatingTypePicker.SelectedItem as string,
                AcType = AcTypePicker.SelectedItem as string,
                FridgeWaterFilterType = FridgeFilterEntry.Text?.Trim(),
                UnderSinkFilterType = UnderSinkFilterEntry.Text?.Trim(),
                WholeHouseFilterType = WholeHouseFilterEntry.Text?.Trim(),
                WaterHeaterType = WaterHeaterTypePicker.SelectedItem as string,
                WaterHeaterSize = WaterHeaterSizeEntry.Text?.Trim(),
                SmokeCoDetectorBatteryType = BatteryTypePicker.SelectedItem as string
            };

            var result = await _apiClient.SaveMaintenanceItemsAsync(dto);
            if (!result.Success)
            {
                ShowError(result.ErrorMessage ?? "Failed to save.");
                return;
            }

            await Navigation.PushAsync(
                Application.Current!.Handler!.MauiContext!.Services.GetRequiredService<WizardVehiclesPage>());
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
