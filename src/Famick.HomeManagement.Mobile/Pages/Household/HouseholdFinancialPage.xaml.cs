using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdFinancialPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;

    public HouseholdFinancialPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        ShowLoading();

        try
        {
            var result = await _apiClient.GetHomeAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _home = result.Data;
                    PopulateData();
                    ShowContent();
                }
                else
                {
                    ShowEmpty();
                }
            });
        }
        catch
        {
            MainThread.BeginInvokeOnMainThread(ShowEmpty);
        }
    }

    private void PopulateData()
    {
        if (_home == null) return;

        // Insurance
        InsuranceTypeLabel.Text = InsuranceTypeHelper.GetDisplayName(_home.InsuranceType);
        PolicyNumberLabel.Text = _home.InsurancePolicyNumber ?? "--";
        AgentNameLabel.Text = _home.InsuranceAgentName ?? "--";
        AgentPhoneLabel.Text = _home.InsuranceAgentPhone ?? "--";
        AgentEmailLabel.Text = _home.InsuranceAgentEmail ?? "--";

        // Financial
        MortgageLabel.Text = _home.MortgageInfo ?? "--";
        TaxAccountLabel.Text = _home.PropertyTaxAccountNumber ?? "--";
        AppraisalValueLabel.Text = _home.AppraisalValue.HasValue
            ? _home.AppraisalValue.Value.ToString("C0") : "--";
        AppraisalDateLabel.Text = _home.AppraisalDate.HasValue
            ? _home.AppraisalDate.Value.ToString("MMM d, yyyy") : "--";
        EscrowLabel.Text = _home.EscrowDetails ?? "--";
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(HouseholdFinancialEditPage));
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}
