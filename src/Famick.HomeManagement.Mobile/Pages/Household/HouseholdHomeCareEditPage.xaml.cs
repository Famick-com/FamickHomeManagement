using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdHomeCareEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;
    private bool _loaded;

    public HouseholdHomeCareEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;

        try
        {
            var result = await _apiClient.GetHomeAsync();
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _home = result.Data;
                    PopulateForm();
                }
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
            });
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                LoadingIndicator.IsVisible = false;
                LoadingIndicator.IsRunning = false;
                ContentScroll.IsVisible = true;
                _ = DisplayAlert("Error", $"Failed to load: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_home == null) return;
        AcFilterSizesEntry.Text = _home.AcFilterSizes;
        AcFilterIntervalEntry.Text = _home.AcFilterReplacementIntervalDays?.ToString();
        FridgeFilterEntry.Text = _home.FridgeWaterFilterType;
        UnderSinkFilterEntry.Text = _home.UnderSinkFilterType;
        WholeHouseFilterEntry.Text = _home.WholeHouseFilterType;
        BatteryTypeEntry.Text = _home.SmokeCoDetectorBatteryType;
        HvacScheduleEntry.Text = _home.HvacServiceSchedule;
        PestScheduleEntry.Text = _home.PestControlSchedule;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        SaveToolbarItem.IsEnabled = false;

        try
        {
            int.TryParse(AcFilterIntervalEntry.Text, out var interval);

            var request = new UpdateHomeMobileRequest
            {
                // Pass through overview fields
                Unit = _home?.Unit,
                YearBuilt = _home?.YearBuilt,
                SquareFootage = _home?.SquareFootage,
                Bedrooms = _home?.Bedrooms,
                Bathrooms = _home?.Bathrooms,
                HoaName = _home?.HoaName,
                HoaContactInfo = _home?.HoaContactInfo,
                HoaRulesLink = _home?.HoaRulesLink,
                // Home care fields being edited
                AcFilterSizes = AcFilterSizesEntry.Text?.Trim(),
                AcFilterReplacementIntervalDays = interval > 0 ? interval : null,
                FridgeWaterFilterType = FridgeFilterEntry.Text?.Trim(),
                UnderSinkFilterType = UnderSinkFilterEntry.Text?.Trim(),
                WholeHouseFilterType = WholeHouseFilterEntry.Text?.Trim(),
                SmokeCoDetectorBatteryType = BatteryTypeEntry.Text?.Trim(),
                HvacServiceSchedule = HvacScheduleEntry.Text?.Trim(),
                PestControlSchedule = PestScheduleEntry.Text?.Trim(),
                // Pass through financial fields
                InsuranceType = _home?.InsuranceType,
                InsurancePolicyNumber = _home?.InsurancePolicyNumber,
                InsuranceAgentName = _home?.InsuranceAgentName,
                InsuranceAgentPhone = _home?.InsuranceAgentPhone,
                InsuranceAgentEmail = _home?.InsuranceAgentEmail,
                MortgageInfo = _home?.MortgageInfo,
                PropertyTaxAccountNumber = _home?.PropertyTaxAccountNumber,
                EscrowDetails = _home?.EscrowDetails,
                AppraisalValue = _home?.AppraisalValue,
                AppraisalDate = _home?.AppraisalDate
            };

            var result = await _apiClient.UpdateHomeAsync(request);
            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to save", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }
}
