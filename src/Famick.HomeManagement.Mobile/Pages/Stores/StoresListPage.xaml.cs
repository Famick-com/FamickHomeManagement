using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Stores;

public partial class StoresListPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;

    public ObservableCollection<StoreListItem> Stores { get; } = new();

    public StoresListPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        StoresCollection.ItemsSource = Stores;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStoresAsync();
    }

    private async Task LoadStoresAsync()
    {
        ShowLoading();

        var result = await _apiClient.GetShoppingLocationsAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            Stores.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var store in result.Data)
                    Stores.Add(StoreListItem.FromSummary(store));

                if (Stores.Count > 0)
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

    private async void OnStoreSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is StoreListItem item)
        {
            StoresCollection.SelectedItem = null;
            await Shell.Current.GoToAsync(nameof(StoreDetailPage),
                new Dictionary<string, object> { ["StoreId"] = item.Id.ToString() });
        }
    }

    private async void OnDeleteSwiped(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { BindingContext: StoreListItem item })
        {
            var confirmed = await DisplayAlert("Delete Store",
                $"Are you sure you want to delete \"{item.Name}\"?", "Delete", "Cancel");
            if (!confirmed) return;

            var result = await _apiClient.DeleteShoppingLocationAsync(item.Id);
            if (result.Success)
            {
                await LoadStoresAsync();
            }
            else
            {
                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete store", "OK");
            }
        }
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadStoresAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var action = await DisplayActionSheet("Add Store", "Cancel", null,
            "Add Manually", "Add from Store Integration");

        if (action == "Add Manually")
        {
            await Shell.Current.GoToAsync(nameof(StoreEditPage));
        }
        else if (action == "Add from Store Integration")
        {
            await Shell.Current.GoToAsync(nameof(StoreIntegrationLinkPage));
        }
    }

    private void ShowLoading() { LoadingIndicator.IsVisible = true; RefreshContainer.IsVisible = false; EmptyState.IsVisible = false; }
    private void ShowContent() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = true; EmptyState.IsVisible = false; }
    private void ShowEmpty() { LoadingIndicator.IsVisible = false; RefreshContainer.IsVisible = false; EmptyState.IsVisible = true; }
}

/// <summary>
/// View model for store list items with computed display properties.
/// </summary>
public class StoreListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? StoreAddress { get; set; }
    public string? IntegrationType { get; set; }
    public bool IsConnected { get; set; }
    public bool HasIntegration => !string.IsNullOrEmpty(IntegrationType);

    public string SubtitleDisplay =>
        !string.IsNullOrEmpty(StoreAddress) ? StoreAddress :
        !string.IsNullOrEmpty(Description) ? Description :
        "Manual store";

    public bool RequiresReauth { get; set; }

    public string IntegrationBadgeText =>
        IsConnected ? "Connected"
        : RequiresReauth ? "Re-auth needed"
        : "Disconnected";

    public Color IntegrationBadgeColor =>
        IsConnected ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF9800");

    public static StoreListItem FromSummary(StoreSummary s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Description = s.Description,
        StoreAddress = s.StoreAddress,
        IntegrationType = s.IntegrationType,
        IsConnected = s.IsConnected,
        RequiresReauth = s.RequiresReauth
    };
}
