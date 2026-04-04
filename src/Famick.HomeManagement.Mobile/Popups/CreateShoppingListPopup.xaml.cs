using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Popups;

public partial class CreateShoppingListPopup : Popup<CreateShoppingListResult>
{
    private readonly ShoppingApiClient _apiClient;
    private readonly List<StoreSummary> _stores;

    public CreateShoppingListPopup(List<StoreSummary> stores, ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _stores = stores;

        StorePicker.ItemsSource = _stores.Select(s => s.Name).ToList();
        if (_stores.Count > 0)
            StorePicker.SelectedIndex = 0;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
        => await CloseAsync(null!);

    private async void OnCreateClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return;

        if (StorePicker.SelectedIndex < 0 || StorePicker.SelectedIndex >= _stores.Count)
            return;

        var store = _stores[StorePicker.SelectedIndex];

        await CloseAsync(new CreateShoppingListResult(
            name,
            DescriptionEntry.Text?.Trim(),
            store.Id));
    }

    private void OnAddNewStoreClicked(object? sender, EventArgs e)
    {
        NewStoreSection.IsVisible = true;
        AddStoreButton.IsVisible = false;
        NewStoreNameEntry.Focus();
    }

    private void OnCancelNewStoreClicked(object? sender, EventArgs e)
    {
        NewStoreSection.IsVisible = false;
        AddStoreButton.IsVisible = true;
        NewStoreNameEntry.Text = string.Empty;
        NewStoreAddressEntry.Text = string.Empty;
    }

    private async void OnSaveNewStoreClicked(object? sender, EventArgs e)
    {
        var storeName = NewStoreNameEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(storeName))
            return;

        SaveStoreButton.IsEnabled = false;
        SaveStoreButton.Text = "Adding...";

        var request = new CreateStoreRequest
        {
            Name = storeName,
            StoreAddress = NewStoreAddressEntry.Text?.Trim()
        };

        var result = await _apiClient.CreateShoppingLocationAsync(request);
        if (result.Success && result.Data != null)
        {
            var newStore = new StoreSummary
            {
                Id = result.Data.Id,
                Name = result.Data.Name,
                StoreAddress = result.Data.StoreAddress
            };
            _stores.Add(newStore);

            StorePicker.ItemsSource = _stores.Select(s => s.Name).ToList();
            StorePicker.SelectedIndex = _stores.Count - 1;

            NewStoreSection.IsVisible = false;
            AddStoreButton.IsVisible = true;
            NewStoreNameEntry.Text = string.Empty;
            NewStoreAddressEntry.Text = string.Empty;
        }

        SaveStoreButton.IsEnabled = true;
        SaveStoreButton.Text = "Add Store";
    }
}
