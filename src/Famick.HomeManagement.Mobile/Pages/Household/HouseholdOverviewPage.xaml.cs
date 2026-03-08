using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdOverviewPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private MobileHomeDto? _home;

    public HouseholdOverviewPage(ShoppingApiClient apiClient)
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
            var homeTask = _apiClient.GetHomeAsync();
            var tenantTask = _apiClient.GetTenantAsync();
            await Task.WhenAll(homeTask, tenantTask);

            var homeResult = homeTask.Result;
            var tenantResult = tenantTask.Result;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (homeResult.Success && homeResult.Data != null)
                {
                    _home = homeResult.Data;

                    // Household name from tenant
                    if (tenantResult.Success && tenantResult.Data != null)
                    {
                        HouseholdNameLabel.Text = tenantResult.Data.Name ?? "My Home";
                    }
                    else
                    {
                        HouseholdNameLabel.Text = "My Home";
                    }
                    AddressLabel.IsVisible = false;

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

        UnitLabel.Text = _home.Unit ?? "--";
        YearBuiltLabel.Text = _home.YearBuilt?.ToString() ?? "--";
        SqFtLabel.Text = _home.SquareFootage?.ToString("N0") ?? "--";
        BedroomsLabel.Text = _home.Bedrooms?.ToString() ?? "--";
        BathroomsLabel.Text = _home.Bathrooms?.ToString("0.#") ?? "--";

        // HOA
        var hasHoa = !string.IsNullOrEmpty(_home.HoaName);
        HoaSectionLabel.IsVisible = hasHoa;
        HoaCard.IsVisible = hasHoa;

        if (hasHoa)
        {
            HoaNameLabel.Text = _home.HoaName;

            HoaContactStack.IsVisible = !string.IsNullOrEmpty(_home.HoaContactInfo);
            HoaContactLabel.Text = _home.HoaContactInfo;

            HoaRulesStack.IsVisible = !string.IsNullOrEmpty(_home.HoaRulesLink);
            HoaRulesLabel.Text = _home.HoaRulesLink;
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(HouseholdOverviewEditPage));
    }

    private async void OnHoaRulesLinkTapped(object? sender, TappedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_home?.HoaRulesLink))
        {
            try { await Launcher.OpenAsync(new Uri(_home.HoaRulesLink)); }
            catch { await DisplayAlert("Error", "Could not open link", "OK"); }
        }
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
