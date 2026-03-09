using System.Collections.ObjectModel;
using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Settings;

public partial class StorageLocationsPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    public ObservableCollection<LocationDto> Locations { get; } = new();

    public StorageLocationsPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        LocationsCollection.ItemsSource = Locations;
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
            var result = await _apiClient.GetLocationsAsync();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Locations.Clear();
                if (result.Success && result.Data != null)
                {
                    foreach (var loc in result.Data.OrderBy(l => l.SortOrder).ThenBy(l => l.Name))
                        Locations.Add(loc);

                    if (Locations.Count > 0)
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
        var defaultSortOrder = Locations.Count > 0 ? Locations.Max(l => l.SortOrder) + 1 : 0;
        var popup = new LocationPopup();

        var popupResult = await this.ShowPopupAsync<LocationPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
        var result = popupResult.Result;

        var request = new CreateLocationMobileRequest
        {
            Name = result.Name,
            Description = result.Description,
            SortOrder = result.SortOrder == 0 ? defaultSortOrder : result.SortOrder,
            IsActive = result.IsActive
        };

        var apiResult = await _apiClient.CreateLocationAsync(request);
        if (apiResult.Success)
        {
            await LoadAsync();
        }
        else
        {
            await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to add location", "OK");
        }
    }

    private async void OnLocationTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: LocationDto location })
        {
            var popup = new LocationPopup(location);
            var popupResult = await this.ShowPopupAsync<LocationPopupResult>(popup, PopupOptions.Empty, CancellationToken.None);
            if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;
            var result = popupResult.Result;

            var request = new UpdateLocationMobileRequest
            {
                Name = result.Name,
                Description = result.Description,
                SortOrder = result.SortOrder,
                IsActive = result.IsActive
            };

            var apiResult = await _apiClient.UpdateLocationAsync(location.Id, request);
            if (apiResult.Success)
            {
                await LoadAsync();
            }
            else
            {
                await DisplayAlert("Error", apiResult.ErrorMessage ?? "Failed to update location", "OK");
            }
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: LocationDto location })
        {
            var confirm = await DisplayAlert("Delete", $"Delete {location.Name}?", "Delete", "Cancel");
            if (!confirm) return;

            var result = await _apiClient.DeleteLocationAsync(location.Id);
            if (result.Success)
            {
                await LoadAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete location", "OK");
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
