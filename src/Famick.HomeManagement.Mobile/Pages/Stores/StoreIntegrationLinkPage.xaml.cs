using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Stores;

[QueryProperty(nameof(ShoppingLocationId), "ShoppingLocationId")]
public partial class StoreIntegrationLinkPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly LocationService _locationService;
    private readonly StoreIntegrationOAuthService _oauthService;

    private StoreIntegrationPlugin? _selectedPlugin;
    private double? _locationLat;
    private double? _locationLng;

    public string ShoppingLocationId { get; set; } = string.Empty;

    public ObservableCollection<PluginListItem> Plugins { get; } = new();
    public ObservableCollection<StoreSearchResult> StoreResults { get; } = new();

    public StoreIntegrationLinkPage(
        ShoppingApiClient apiClient,
        LocationService locationService,
        StoreIntegrationOAuthService oauthService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _locationService = locationService;
        _oauthService = oauthService;
        PluginsCollection.ItemsSource = Plugins;
        StoreResultsCollection.ItemsSource = StoreResults;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPluginsAsync();
    }

    private async Task LoadPluginsAsync()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        Step1Content.IsVisible = false;

        var result = await _apiClient.GetStoreIntegrationPluginsAsync();

        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            Step1Content.IsVisible = true;

            Plugins.Clear();
            if (result.Success && result.Data != null)
            {
                foreach (var plugin in result.Data.Where(p => p.IsAvailable))
                    Plugins.Add(PluginListItem.FromPlugin(plugin));

                NoPluginsLabel.IsVisible = Plugins.Count == 0;
            }
            else
            {
                NoPluginsLabel.IsVisible = true;
            }
        });
    }

    private void OnPluginSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is PluginListItem item)
        {
            PluginsCollection.SelectedItem = null;
            _selectedPlugin = new StoreIntegrationPlugin
            {
                PluginId = item.PluginId,
                DisplayName = item.DisplayName,
                IsAvailable = true,
                IsConnected = item.IsConnected
            };

            GoToStep(2);
            PluginNameLabel.Text = $"Search for {item.DisplayName} stores near you.";
        }
    }

    private async void OnUseMyLocationClicked(object? sender, EventArgs e)
    {
        try
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location != null)
            {
                _locationLat = location.Latitude;
                _locationLng = location.Longitude;
                ZipCodeEntry.Text = "";
                ZipCodeEntry.Placeholder = $"Using GPS ({location.Latitude:F2}, {location.Longitude:F2})";
                await SearchStoresAsync();
            }
            else
            {
                await DisplayAlert("Location", "Unable to get your current location. Please enter a ZIP code.", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Location Error", ex.Message, "OK");
        }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        var zipCode = ZipCodeEntry.Text?.Trim();
        if (string.IsNullOrEmpty(zipCode) && !_locationLat.HasValue)
        {
            await DisplayAlert("Search", "Please enter a ZIP code or use your location.", "OK");
            return;
        }

        // If ZIP code entered, clear lat/lng
        if (!string.IsNullOrEmpty(zipCode))
        {
            _locationLat = null;
            _locationLng = null;
        }

        await SearchStoresAsync();
    }

    private async Task SearchStoresAsync()
    {
        if (_selectedPlugin == null) return;

        SearchingIndicator.IsVisible = true;
        SearchingIndicator.IsRunning = true;
        SearchButton.IsEnabled = false;

        var zipCode = ZipCodeEntry.Text?.Trim();
        var result = await _apiClient.SearchIntegrationStoresAsync(
            _selectedPlugin.PluginId, zipCode, _locationLat, _locationLng);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            SearchingIndicator.IsVisible = false;
            SearchingIndicator.IsRunning = false;
            SearchButton.IsEnabled = true;

            StoreResults.Clear();
            if (result.Success && result.Data != null && result.Data.Count > 0)
            {
                foreach (var store in result.Data)
                    StoreResults.Add(store);

                GoToStep(3);
                NoResultsLabel.IsVisible = false;
            }
            else
            {
                GoToStep(3);
                NoResultsLabel.IsVisible = true;
            }
        });
    }

    private void UpdateAddButtonVisibility()
    {
        AddStoreButton.IsVisible = StoreResultsCollection.SelectedItem != null;
    }

    private async void OnAddStoreClicked(object? sender, EventArgs e)
    {
        if (StoreResultsCollection.SelectedItem is not StoreSearchResult selectedStore || _selectedPlugin == null)
        {
            await DisplayAlert("Selection", "Please select a store from the list.", "OK");
            return;
        }

        AddStoreButton.IsEnabled = false;

        try
        {
            Guid shoppingLocationId;

            // If we have an existing shopping location ID, use it; otherwise create a new one
            if (!string.IsNullOrEmpty(ShoppingLocationId) && Guid.TryParse(ShoppingLocationId, out var existingId))
            {
                shoppingLocationId = existingId;
            }
            else
            {
                // Create a new shopping location using the store name
                var createResult = await _apiClient.CreateShoppingLocationAsync(new CreateStoreRequest
                {
                    Name = selectedStore.Name,
                    StoreAddress = selectedStore.FullAddress,
                    StorePhone = selectedStore.Phone,
                    Latitude = selectedStore.Latitude,
                    Longitude = selectedStore.Longitude
                });

                if (!createResult.Success || createResult.Data == null)
                {
                    await DisplayAlert("Error", createResult.ErrorMessage ?? "Failed to create store", "OK");
                    AddStoreButton.IsEnabled = true;
                    return;
                }

                shoppingLocationId = createResult.Data.Id;
            }

            // Link the store to the integration
            var linkResult = await _apiClient.LinkStoreLocationAsync(shoppingLocationId, new LinkStoreRequest
            {
                PluginId = _selectedPlugin.PluginId,
                ExternalLocationId = selectedStore.ExternalLocationId,
                ExternalChainId = selectedStore.ChainId,
                StoreName = selectedStore.Name,
                StoreAddress = selectedStore.FullAddress,
                StorePhone = selectedStore.Phone,
                Latitude = selectedStore.Latitude,
                Longitude = selectedStore.Longitude
            });

            if (!linkResult.Success)
            {
                await DisplayAlert("Error", linkResult.ErrorMessage ?? "Failed to link store", "OK");
                AddStoreButton.IsEnabled = true;
                return;
            }

            // Auto-start OAuth if the plugin is not already connected
            if (!_selectedPlugin.IsConnected)
            {
                var oauthResult = await _oauthService.ConnectStoreAsync(_selectedPlugin.PluginId, shoppingLocationId);
                if (oauthResult.Success)
                {
                    await DisplayAlert("Success", "Store added and connected successfully!", "OK");
                }
                else if (!oauthResult.WasCancelled)
                {
                    await DisplayAlert("Partial Success",
                        "Store was added and linked, but the OAuth connection failed. You can connect from the store detail page.",
                        "OK");
                }
                // If cancelled, still continue - store was created/linked
            }

            // Navigate back
            await Shell.Current.GoToAsync("../..");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            AddStoreButton.IsEnabled = true;
        }
    }

    private void GoToStep(int step)
    {
        var activeColor = Color.FromArgb("#1976D2");
        var inactiveColor = Application.Current?.RequestedTheme == AppTheme.Dark
            ? Color.FromArgb("#424242") : Color.FromArgb("#E0E0E0");

        Step1Indicator.BackgroundColor = step >= 1 ? activeColor : inactiveColor;
        Step2Indicator.BackgroundColor = step >= 2 ? activeColor : inactiveColor;
        Step3Indicator.BackgroundColor = step >= 3 ? activeColor : inactiveColor;

        Step1Content.IsVisible = step == 1;
        Step2Content.IsVisible = step == 2;
        Step3Content.IsVisible = step == 3;

        // Show add button visibility based on selection
        if (step == 3)
        {
            StoreResultsCollection.SelectionChanged += (_, _) => UpdateAddButtonVisibility();
        }
    }
}

/// <summary>
/// View model for plugin list items.
/// </summary>
public class PluginListItem
{
    public string PluginId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public bool RequiresReauth { get; set; }

    public string StatusText =>
        RequiresReauth ? "Requires re-authentication" :
        IsConnected ? "Connected" : "Not connected";

    public Color StatusColor =>
        RequiresReauth ? Color.FromArgb("#FF9800") :
        IsConnected ? Color.FromArgb("#4CAF50") : Colors.Gray;

    public static PluginListItem FromPlugin(StoreIntegrationPlugin p) => new()
    {
        PluginId = p.PluginId,
        DisplayName = p.DisplayName,
        IsConnected = p.IsConnected,
        RequiresReauth = p.RequiresReauth
    };
}
