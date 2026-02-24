using System.Collections.ObjectModel;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

[QueryProperty(nameof(LocationId), "LocationId")]
[QueryProperty(nameof(StoreName), "StoreName")]
public partial class AisleOrderPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly ConnectivityService _connectivityService;

    private Guid _locationId;
    private bool _hasChanges;

    public string LocationId
    {
        set => _locationId = Guid.Parse(value);
    }

    public string StoreName
    {
        set => Title = "Edit Aisle Order";
    }

    public ObservableCollection<AisleItem> Aisles { get; } = new();

    public AisleOrderPage(
        ShoppingApiClient apiClient,
        ConnectivityService connectivityService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _connectivityService = connectivityService;

        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAisleOrderAsync();
    }

    private async Task LoadAisleOrderAsync()
    {
        if (!_connectivityService.IsOnline)
        {
            await DisplayAlertAsync("Offline", "You need to be online to edit aisle order.", "OK");
            await Shell.Current.GoToAsync("..");
            return;
        }

        ShowLoading(true);

        try
        {
            var result = await _apiClient.GetAisleOrderAsync(_locationId);

            if (result.Success && result.Data != null)
            {
                Aisles.Clear();

                // Build the aisle list:
                // - If custom order exists, use it first
                // - Then append any known aisles not already in the custom order (new aisles)
                List<string> aisleList;
                if (result.Data.OrderedAisles.Count > 0)
                {
                    // Start with custom ordered aisles
                    aisleList = new List<string>(result.Data.OrderedAisles);

                    // Add any known aisles not in the custom order (newly discovered aisles)
                    var missingAisles = result.Data.KnownAisles
                        .Where(k => !result.Data.OrderedAisles.Contains(k))
                        .ToList();

                    aisleList.AddRange(missingAisles);
                }
                else
                {
                    // No custom order - use known aisles
                    aisleList = result.Data.KnownAisles;
                }

                foreach (var aisle in aisleList)
                {
                    Aisles.Add(new AisleItem { Value = aisle });
                }

                AisleList.ItemsSource = Aisles;

                if (Aisles.Count == 0)
                {
                    AisleList.IsVisible = false;
                    NoAislesView.IsVisible = true;
                    ResetButton.IsEnabled = false;
                }
                else
                {
                    AisleList.IsVisible = true;
                    NoAislesView.IsVisible = false;
                    ResetButton.IsEnabled = true;
                }
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to load aisle order", "OK");
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to load aisle order: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("..");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void OnMoveUpClicked(object? sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is AisleItem item)
        {
            var index = Aisles.IndexOf(item);
            if (index > 0)
            {
                Aisles.Move(index, index - 1);
                _hasChanges = true;
                AisleList.ItemsSource = null;
                AisleList.ItemsSource = Aisles;
            }
        }
    }

    private void OnMoveDownClicked(object? sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is AisleItem item)
        {
            var index = Aisles.IndexOf(item);
            if (index >= 0 && index < Aisles.Count - 1)
            {
                Aisles.Move(index, index + 1);
                _hasChanges = true;
                AisleList.ItemsSource = null;
                AisleList.ItemsSource = Aisles;
            }
        }
    }

    private void OnMoveToTopClicked(object? sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is AisleItem item)
        {
            var index = Aisles.IndexOf(item);
            if (index > 0)
            {
                Aisles.Move(index, 0);
                _hasChanges = true;
                AisleList.ItemsSource = null;
                AisleList.ItemsSource = Aisles;
            }
        }
    }

    private void OnMoveToBottomClicked(object? sender, EventArgs e)
    {
        if (sender is ImageButton button && button.CommandParameter is AisleItem item)
        {
            var index = Aisles.IndexOf(item);
            if (index >= 0 && index < Aisles.Count - 1)
            {
                Aisles.Move(index, Aisles.Count - 1);
                _hasChanges = true;
                AisleList.ItemsSource = null;
                AisleList.ItemsSource = Aisles;
            }
        }
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        if (!_hasChanges)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        if (!_connectivityService.IsOnline)
        {
            await DisplayAlertAsync("Offline", "You need to be online to save changes.", "OK");
            return;
        }

        ShowLoading(true);

        try
        {
            var orderedList = Aisles.Select(a => a.Value).ToList();

            var request = new UpdateAisleOrderRequest
            {
                OrderedAisles = orderedList
            };

            var result = await _apiClient.UpdateAisleOrderAsync(_locationId, request);

            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to save aisle order", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to save: {ex.Message}", "OK");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private async void OnResetClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Reset Aisle Order",
            "Reset to default order (numeric then alphabetical)?",
            "Reset",
            "Cancel");

        if (!confirm) return;

        if (!_connectivityService.IsOnline)
        {
            await DisplayAlertAsync("Offline", "You need to be online to reset aisle order.", "OK");
            return;
        }

        ShowLoading(true);

        try
        {
            var result = await _apiClient.ResetAisleOrderAsync(_locationId);

            if (result.Success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to reset aisle order", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Failed to reset: {ex.Message}", "OK");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.IsRunning = show;
        LoadingIndicator.IsVisible = show;
    }
}

/// <summary>
/// Represents an aisle in the order list.
/// </summary>
public class AisleItem
{
    public string Value { get; set; } = "";

    public string DisplayName => int.TryParse(Value, out _)
        ? $"Aisle {Value}"
        : Value;
}
