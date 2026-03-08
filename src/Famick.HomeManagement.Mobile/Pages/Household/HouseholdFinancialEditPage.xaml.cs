using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdFinancialEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;
    private bool _loaded;
    private bool _hasAppraisalDate;

    public HouseholdFinancialEditPage(ShoppingApiClient apiClient)
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

        // Insurance type picker
        if (_home.InsuranceType.HasValue && _home.InsuranceType.Value >= 0 && _home.InsuranceType.Value <= 1)
            InsuranceTypePicker.SelectedIndex = _home.InsuranceType.Value;

        PolicyNumberEntry.Text = _home.InsurancePolicyNumber;
        AgentNameEntry.Text = _home.InsuranceAgentName;
        AgentPhoneEntry.Text = _home.InsuranceAgentPhone;
        AgentEmailEntry.Text = _home.InsuranceAgentEmail;
        MortgageEditor.Text = _home.MortgageInfo;
        TaxAccountEntry.Text = _home.PropertyTaxAccountNumber;
        AppraisalValueEntry.Text = _home.AppraisalValue?.ToString("0");
        EscrowEditor.Text = _home.EscrowDetails;

        if (_home.AppraisalDate.HasValue)
        {
            _hasAppraisalDate = true;
            AppraisalDatePicker.Date = _home.AppraisalDate.Value.Date;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        SaveToolbarItem.IsEnabled = false;

        try
        {
            decimal.TryParse(AppraisalValueEntry.Text, out var appraisalValue);

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
                // Pass through home care fields
                AcFilterSizes = _home?.AcFilterSizes,
                AcFilterReplacementIntervalDays = _home?.AcFilterReplacementIntervalDays,
                FridgeWaterFilterType = _home?.FridgeWaterFilterType,
                UnderSinkFilterType = _home?.UnderSinkFilterType,
                WholeHouseFilterType = _home?.WholeHouseFilterType,
                SmokeCoDetectorBatteryType = _home?.SmokeCoDetectorBatteryType,
                HvacServiceSchedule = _home?.HvacServiceSchedule,
                PestControlSchedule = _home?.PestControlSchedule,
                // Financial fields being edited
                InsuranceType = InsuranceTypePicker.SelectedIndex >= 0
                    ? InsuranceTypePicker.SelectedIndex : null,
                InsurancePolicyNumber = PolicyNumberEntry.Text?.Trim(),
                InsuranceAgentName = AgentNameEntry.Text?.Trim(),
                InsuranceAgentPhone = AgentPhoneEntry.Text?.Trim(),
                InsuranceAgentEmail = AgentEmailEntry.Text?.Trim(),
                MortgageInfo = MortgageEditor.Text?.Trim(),
                PropertyTaxAccountNumber = TaxAccountEntry.Text?.Trim(),
                AppraisalValue = appraisalValue > 0 ? appraisalValue : null,
                AppraisalDate = _hasAppraisalDate || appraisalValue > 0
                    ? AppraisalDatePicker.Date : null,
                EscrowDetails = EscrowEditor.Text?.Trim()
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
