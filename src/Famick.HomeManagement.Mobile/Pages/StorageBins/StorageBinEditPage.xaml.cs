using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.StorageBins;

[QueryProperty(nameof(StorageBinId), "StorageBinId")]
public partial class StorageBinEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private StorageBinDetailItem? _bin;
    private bool _isEditMode;
    private bool _loaded;
    private List<LocationDto> _locations = new();

    public string StorageBinId { get; set; } = string.Empty;

    public StorageBinEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded) return;
        _loaded = true;

        _isEditMode = !string.IsNullOrEmpty(StorageBinId) && Guid.TryParse(StorageBinId, out _);

        await LoadLocationsAsync();

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Storage Bin";
            await LoadBinAsync();
        }
        else
        {
            TitleLabel.Text = "New Storage Bin";
        }
    }

    private async Task LoadLocationsAsync()
    {
        var result = await _apiClient.GetLocationsAsync();
        if (result.Success && result.Data != null)
        {
            _locations = result.Data;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var names = new List<string> { "(None)" };
                names.AddRange(_locations.Select(l => l.Name));
                LocationPicker.ItemsSource = names;
                LocationPicker.SelectedIndex = 0;
            });
        }
    }

    private async Task LoadBinAsync()
    {
        if (!Guid.TryParse(StorageBinId, out var id)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;

        try
        {
            var result = await _apiClient.GetStorageBinAsync(id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _bin = result.Data;
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
                _ = DisplayAlert("Error", $"Failed to load storage bin: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_bin == null) return;

        ShortCodeLabel.Text = _bin.ShortCode;
        ShortCodeSection.IsVisible = true;

        DescriptionEditor.Text = _bin.Description;
        CategoryEntry.Text = _bin.Category;

        if (_bin.LocationId.HasValue)
        {
            var locIndex = _locations.FindIndex(l => l.Id == _bin.LocationId.Value);
            LocationPicker.SelectedIndex = locIndex >= 0 ? locIndex + 1 : 0;
        }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        Guid? locationId = null;
        if (LocationPicker.SelectedIndex > 0)
        {
            locationId = _locations[LocationPicker.SelectedIndex - 1].Id;
        }

        SaveToolbarItem.IsEnabled = false;

        try
        {
            if (_isEditMode && _bin != null)
            {
                var request = new UpdateStorageBinMobileRequest
                {
                    Description = DescriptionEditor.Text?.Trim(),
                    LocationId = locationId,
                    Category = CategoryEntry.Text?.Trim()
                };

                var result = await _apiClient.UpdateStorageBinAsync(_bin.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update storage bin", "OK");
            }
            else
            {
                var request = new CreateStorageBinMobileRequest
                {
                    Description = DescriptionEditor.Text?.Trim(),
                    LocationId = locationId,
                    Category = CategoryEntry.Text?.Trim()
                };

                var result = await _apiClient.CreateStorageBinAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create storage bin", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }
}
