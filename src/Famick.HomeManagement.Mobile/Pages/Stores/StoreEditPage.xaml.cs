using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Stores;

[QueryProperty(nameof(StoreId), "StoreId")]
public partial class StoreEditPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private ShoppingLocationDetail? _store;
    private bool _isEditMode;
    private bool _loaded;

    public string StoreId { get; set; } = string.Empty;

    public StoreEditPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_loaded) return;
        _loaded = true;

        _isEditMode = !string.IsNullOrEmpty(StoreId) && Guid.TryParse(StoreId, out _);

        if (_isEditMode)
        {
            TitleLabel.Text = "Edit Store";
            await LoadStoreAsync();
        }
        else
        {
            TitleLabel.Text = "New Store";
        }
    }

    private async Task LoadStoreAsync()
    {
        if (!Guid.TryParse(StoreId, out var id)) return;

        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;

        try
        {
            var result = await _apiClient.GetShoppingLocationAsync(id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (result.Success && result.Data != null)
                {
                    _store = result.Data;
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
                _ = DisplayAlert("Error", $"Failed to load store: {ex.Message}", "OK");
            });
        }
    }

    private void PopulateForm()
    {
        if (_store == null) return;

        NameEntry.Text = _store.Name;
        DescriptionEditor.Text = _store.Description;
        AddressEntry.Text = _store.StoreAddress;
        PhoneEntry.Text = _store.StorePhone;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await DisplayAlert("Validation", "Store name is required.", "OK");
            return;
        }

        SaveToolbarItem.IsEnabled = false;

        try
        {
            if (_isEditMode && _store != null)
            {
                var request = new UpdateStoreRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    StoreAddress = AddressEntry.Text?.Trim(),
                    StorePhone = PhoneEntry.Text?.Trim()
                };

                var result = await _apiClient.UpdateShoppingLocationAsync(_store.Id, request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to update store", "OK");
            }
            else
            {
                var request = new CreateStoreRequest
                {
                    Name = name,
                    Description = DescriptionEditor.Text?.Trim(),
                    StoreAddress = AddressEntry.Text?.Trim(),
                    StorePhone = PhoneEntry.Text?.Trim()
                };

                var result = await _apiClient.CreateShoppingLocationAsync(request);
                if (result.Success)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                await DisplayAlert("Error", result.ErrorMessage ?? "Failed to create store", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
        }

        SaveToolbarItem.IsEnabled = true;
    }
}
