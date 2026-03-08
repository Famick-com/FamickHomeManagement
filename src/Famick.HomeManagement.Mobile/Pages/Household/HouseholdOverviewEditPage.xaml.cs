using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdOverviewEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;
    private bool _loaded;

    public HouseholdOverviewEditPage(ShoppingApiClient apiClient)
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
        UnitEntry.Text = _home.Unit;
        YearBuiltEntry.Text = _home.YearBuilt?.ToString();
        SqFtEntry.Text = _home.SquareFootage?.ToString();
        BedroomsEntry.Text = _home.Bedrooms?.ToString();
        BathroomsEntry.Text = _home.Bathrooms?.ToString("0.#");
        HoaNameEntry.Text = _home.HoaName;
        HoaContactEntry.Text = _home.HoaContactInfo;
        HoaRulesEntry.Text = _home.HoaRulesLink;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        SaveToolbarItem.IsEnabled = false;

        try
        {
            var request = BuildRequest();
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

    private UpdateHomeMobileRequest BuildRequest()
    {
        int.TryParse(YearBuiltEntry.Text, out var yearBuilt);
        int.TryParse(SqFtEntry.Text, out var sqFt);
        int.TryParse(BedroomsEntry.Text, out var bedrooms);
        decimal.TryParse(BathroomsEntry.Text, out var bathrooms);

        return new UpdateHomeMobileRequest
        {
            // Overview fields being edited
            Unit = UnitEntry.Text?.Trim(),
            YearBuilt = yearBuilt > 0 ? yearBuilt : null,
            SquareFootage = sqFt > 0 ? sqFt : null,
            Bedrooms = bedrooms > 0 ? bedrooms : null,
            Bathrooms = bathrooms > 0 ? bathrooms : null,
            HoaName = HoaNameEntry.Text?.Trim(),
            HoaContactInfo = HoaContactEntry.Text?.Trim(),
            HoaRulesLink = HoaRulesEntry.Text?.Trim(),
            // Pass through existing values for other sections
            AcFilterSizes = _home?.AcFilterSizes,
            AcFilterReplacementIntervalDays = _home?.AcFilterReplacementIntervalDays,
            FridgeWaterFilterType = _home?.FridgeWaterFilterType,
            UnderSinkFilterType = _home?.UnderSinkFilterType,
            WholeHouseFilterType = _home?.WholeHouseFilterType,
            SmokeCoDetectorBatteryType = _home?.SmokeCoDetectorBatteryType,
            HvacServiceSchedule = _home?.HvacServiceSchedule,
            PestControlSchedule = _home?.PestControlSchedule,
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
    }
}
