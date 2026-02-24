using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

[QueryProperty(nameof(ListId), "ListId")]
[QueryProperty(nameof(ItemId), "ItemId")]
[QueryProperty(nameof(ParentName), "ParentName")]
[QueryProperty(nameof(ParentAmount), "ParentAmount")]
[QueryProperty(nameof(ParentImageUrl), "ParentImageUrl")]
public partial class ChildProductSelectionPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private Guid _listId;
    private Guid _itemId;
    private decimal _parentAmount;
    private decimal _totalPurchased;

    public string ListId
    {
        set => _listId = Guid.Parse(value);
    }

    public string ItemId
    {
        set => _itemId = Guid.Parse(value);
    }

    public string ParentName { get; set; } = "";

    public string ParentAmount
    {
        set => _parentAmount = decimal.TryParse(value, out var amount) ? amount : 1;
    }

    public string? ParentImageUrl { get; set; }

    public ObservableCollection<ChildProductDto> ChildProducts { get; } = new();

    public ChildProductSelectionPage(ShoppingApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Set parent info
        ParentNameLabel.Text = ParentName;
        QuantityLabel.Text = $"Need: {_parentAmount}";

        if (!string.IsNullOrEmpty(ParentImageUrl))
        {
            ParentImage.Source = ParentImageUrl;
            ParentImage.IsVisible = true;
            ParentNoImage.IsVisible = false;
        }

        await LoadChildProductsAsync();
    }

    private async Task LoadChildProductsAsync()
    {
        ShowLoading(true);

        try
        {
            var result = await _apiClient.GetChildProductsAsync(_listId, _itemId);

            if (result.Success && result.Data != null)
            {
                ChildProducts.Clear();
                _totalPurchased = 0;

                foreach (var child in result.Data)
                {
                    ChildProducts.Add(child);
                    _totalPurchased += child.PurchasedQuantity;
                }

                UpdateProgress();
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to load variants", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Connection error: {ex.Message}", "OK");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private void UpdateProgress()
    {
        var progress = _parentAmount > 0 ? (double)(_totalPurchased / _parentAmount) : 0;
        QuantityProgress.Progress = Math.Min(progress, 1.0); // Cap at 100% visually

        if (_totalPurchased >= _parentAmount)
        {
            ProgressLabel.Text = $"✓ Complete ({_totalPurchased}/{_parentAmount})";
            ProgressLabel.TextColor = Colors.Green;
        }
        else
        {
            ProgressLabel.Text = $"{_totalPurchased}/{_parentAmount} purchased";
        }
    }

    private async void OnCheckOffClicked(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.CommandParameter is not ChildProductDto child)
            return;

        button.IsEnabled = false;

        try
        {
            var request = new CheckOffChildRequest
            {
                ChildProductId = child.ProductId,
                Quantity = 1 // Default to 1, could add quantity picker
            };

            var result = await _apiClient.CheckOffChildAsync(_listId, _itemId, request);

            if (result.Success)
            {
                // Update local state
                child.PurchasedQuantity += 1;
                _totalPurchased += 1;
                UpdateProgress();

                // Refresh the collection to update UI
                var index = ChildProducts.IndexOf(child);
                if (index >= 0)
                {
                    ChildProducts.RemoveAt(index);
                    ChildProducts.Insert(index, child);
                }
            }
            else
            {
                await DisplayAlertAsync("Error", result.ErrorMessage ?? "Failed to check off", "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"Connection error: {ex.Message}", "OK");
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private async void OnCheckOffParentClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync(
            "Check Off Parent",
            "This will mark the parent product as purchased without selecting specific variants. Continue?",
            "Yes, Check Off",
            "Cancel");

        if (!confirm) return;

        WeakReferenceMessenger.Default.Send(new CheckOffParentMessage(_itemId));
        await Shell.Current.GoToAsync("..");
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new ChildSelectionDoneMessage(_itemId));
        await Shell.Current.GoToAsync("..");
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadChildProductsAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.IsRunning = show;
        LoadingIndicator.IsVisible = show;
        RefreshContainer.IsVisible = !show;
    }
}
