using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Household;

public partial class HouseholdUtilitiesPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    public ObservableCollection<MobileHomeUtilityDto> Utilities { get; } = new();

    public HouseholdUtilitiesPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        UtilitiesCollection.ItemsSource = Utilities;
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
                Utilities.Clear();
                if (result.Success && result.Data != null)
                {
                    foreach (var u in result.Data.Utilities)
                        Utilities.Add(u);

                    if (Utilities.Count > 0)
                        ShowContent();
                    else
                        ShowEmpty();
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

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var popup = new UtilityPopup();
        var popupResult = await this.ShowPopupAsync<UtilityPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var request = new CreateUtilityMobileRequest
        {
            UtilityType = result.UtilityType,
            CompanyName = result.CompanyName,
            AccountNumber = result.AccountNumber,
            PhoneNumber = result.PhoneNumber,
            Website = result.Website,
            LoginEmail = result.LoginEmail,
            Notes = result.Notes
        };

        var apiResult = await _apiClient.CreateUtilityAsync(request);
        if (apiResult.Success)
        {
            await LoadAsync();
        }
        else
        {
            await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add utility", "OK");
        }
    }

    private async void OnUtilityTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: MobileHomeUtilityDto utility })
        {
            var popup = new UtilityPopup(utility);
            var popupResult = await this.ShowPopupAsync<UtilityPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);
            if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
            var result = popupResult.Result;

            var request = new UpdateUtilityMobileRequest
            {
                CompanyName = result.CompanyName,
                AccountNumber = result.AccountNumber,
                PhoneNumber = result.PhoneNumber,
                Website = result.Website,
                LoginEmail = result.LoginEmail,
                Notes = result.Notes
            };

            var apiResult = await _apiClient.UpdateUtilityAsync(utility.Id, request);
            if (apiResult.Success)
            {
                await LoadAsync();
            }
            else
            {
                await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to update utility", "OK");
            }
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: MobileHomeUtilityDto utility })
        {
            var confirm = await DisplayAlert("Delete", $"Delete {utility.DisplayName}?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteUtilityAsync(utility.Id);
            if (result.Success)
            {
                await LoadAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete utility", "OK");
            }
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
