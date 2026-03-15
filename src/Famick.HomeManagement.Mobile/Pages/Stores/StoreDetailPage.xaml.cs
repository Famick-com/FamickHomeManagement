using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages.Stores;

[QueryProperty(nameof(StoreId), "StoreId")]
public partial class StoreDetailPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly StoreIntegrationOAuthService _oauthService;
    private ShoppingLocationDetail? _store;

    public string StoreId { get; set; } = string.Empty;

    public StoreDetailPage(ShoppingApiClient apiClient, StoreIntegrationOAuthService oauthService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _oauthService = oauthService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStoreAsync();
    }

    private async Task LoadStoreAsync()
    {
        if (!Guid.TryParse(StoreId, out var id))
        {
            MainThread.BeginInvokeOnMainThread(() => ShowError("Invalid store ID"));
            return;
        }

        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var result = await _apiClient.GetShoppingLocationAsync(id);
            if (result.Success && result.Data != null)
            {
                _store = result.Data;
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    RenderStore();
                    ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(result.ErrorMessage ?? "Failed to load store"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
    }

    private void RenderStore()
    {
        if (_store == null) return;

        TitleLabel.Text = _store.Name;

        // Description
        if (!string.IsNullOrEmpty(_store.Description))
        {
            DescriptionLabel.Text = _store.Description;
            DescriptionSection.IsVisible = true;
        }
        else
        {
            DescriptionSection.IsVisible = false;
        }

        // Details
        DetailsStack.Children.Clear();
        AddDetailRow("Address", _store.StoreAddress);
        AddDetailRow("Phone", _store.StorePhone);
        AddDetailRow("Products", _store.ProductCount > 0 ? $"{_store.ProductCount} linked products" : null);

        // Integration section
        RenderIntegrationSection();
    }

    private void RenderIntegrationSection()
    {
        if (_store == null) return;

        // Clear all except the title label
        while (IntegrationStack.Children.Count > 1)
            IntegrationStack.Children.RemoveAt(IntegrationStack.Children.Count - 1);

        if (!_store.HasIntegration)
        {
            // Not linked - show link button
            IntegrationStack.Children.Add(new Label
            {
                Text = "This store is not linked to a store integration.",
                FontSize = 13,
                TextColor = Colors.Gray
            });

            var linkBtn = new Button
            {
                Text = "Link to Store Integration",
                BackgroundColor = Color.FromArgb("#1976D2"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            linkBtn.Clicked += OnLinkIntegrationClicked;
            IntegrationStack.Children.Add(linkBtn);
        }
        else if (_store.IsConnected)
        {
            // Linked + connected
            var statusRow = new HorizontalStackLayout { Spacing = 8 };
            statusRow.Children.Add(new Label
            {
                Text = _store.IntegrationType,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
                VerticalOptions = LayoutOptions.Center
            });
            var badge = new Border
            {
                Padding = new Thickness(6, 2),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#4CAF50"),
                Content = new Label { Text = "Connected", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
            };
            statusRow.Children.Add(badge);
            IntegrationStack.Children.Add(statusRow);

            var disconnectBtn = new Button
            {
                Text = "Disconnect",
                BackgroundColor = Color.FromArgb("#FF9800"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            disconnectBtn.Clicked += OnDisconnectClicked;
            IntegrationStack.Children.Add(disconnectBtn);

            var unlinkBtn = new Button
            {
                Text = "Unlink Store",
                BackgroundColor = Color.FromArgb("#9E9E9E"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            unlinkBtn.Clicked += OnUnlinkClicked;
            IntegrationStack.Children.Add(unlinkBtn);
        }
        else
        {
            // Linked but disconnected or requires re-auth
            var statusRow = new HorizontalStackLayout { Spacing = 8 };
            statusRow.Children.Add(new Label
            {
                Text = _store.IntegrationType,
                FontSize = 14,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
                VerticalOptions = LayoutOptions.Center
            });

            var badgeText = _store.RequiresReauth ? "Requires Re-auth" : "Disconnected";
            var badge = new Border
            {
                Padding = new Thickness(6, 2),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 },
                Stroke = Colors.Transparent,
                BackgroundColor = Color.FromArgb("#FF9800"),
                Content = new Label { Text = badgeText, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
            };
            statusRow.Children.Add(badge);
            IntegrationStack.Children.Add(statusRow);

            var connectBtnText = _store.RequiresReauth ? "Re-authenticate" : "Connect";
            var connectBtn = new Button
            {
                Text = connectBtnText,
                BackgroundColor = Color.FromArgb("#4CAF50"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            connectBtn.Clicked += OnConnectClicked;
            IntegrationStack.Children.Add(connectBtn);

            var unlinkBtn = new Button
            {
                Text = "Unlink Store",
                BackgroundColor = Color.FromArgb("#9E9E9E"),
                TextColor = Colors.White,
                CornerRadius = 8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            unlinkBtn.Clicked += OnUnlinkClicked;
            IntegrationStack.Children.Add(unlinkBtn);
        }
    }

    private void AddDetailRow(string label, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var row = new HorizontalStackLayout { Spacing = 8 };
        row.Children.Add(new Label
        {
            Text = label,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.Gray,
            VerticalOptions = LayoutOptions.Center
        });
        row.Children.Add(new Label
        {
            Text = value,
            FontSize = 14,
            TextColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black,
            VerticalOptions = LayoutOptions.Center
        });
        DetailsStack.Children.Add(row);
    }

    #region Event Handlers

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_store == null) return;
        await Shell.Current.GoToAsync(nameof(StoreEditPage),
            new Dictionary<string, object> { ["StoreId"] = _store.Id.ToString() });
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_store == null) return;

        var confirmed = await DisplayAlert("Delete Store",
            $"Are you sure you want to delete \"{_store.Name}\"?", "Delete", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DeleteShoppingLocationAsync(_store.Id);
        if (result.Success)
        {
            await Shell.Current.GoToAsync("..");
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to delete store", "OK");
        }
    }

    private async void OnLinkIntegrationClicked(object? sender, EventArgs e)
    {
        if (_store == null) return;
        await Shell.Current.GoToAsync(nameof(StoreIntegrationLinkPage),
            new Dictionary<string, object> { ["ShoppingLocationId"] = _store.Id.ToString() });
    }

    private async void OnConnectClicked(object? sender, EventArgs e)
    {
        if (_store == null || string.IsNullOrEmpty(_store.IntegrationType)) return;

        var oauthResult = await _oauthService.ConnectStoreAsync(_store.IntegrationType, _store.Id);
        if (oauthResult.Success)
        {
            // Optimistically update UI immediately so button reflects connected state
            _store.IsConnected = true;
            _store.RequiresReauth = false;
            MainThread.BeginInvokeOnMainThread(RenderIntegrationSection);

            await DisplayAlert("Connected", "Store integration connected successfully.", "OK");
            // Reload from server to confirm
            await LoadStoreAsync();
        }
        else if (!oauthResult.WasCancelled)
        {
            await DisplayAlert("Error", oauthResult.ErrorMessage ?? "Failed to connect", "OK");
        }
    }

    private async void OnDisconnectClicked(object? sender, EventArgs e)
    {
        if (_store == null) return;

        var confirmed = await DisplayAlert("Disconnect",
            "Disconnect this store from its integration? You can reconnect later.", "Disconnect", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.DisconnectStoreAsync(_store.Id);
        if (result.Success)
        {
            await LoadStoreAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to disconnect", "OK");
        }
    }

    private async void OnUnlinkClicked(object? sender, EventArgs e)
    {
        if (_store == null) return;

        var confirmed = await DisplayAlert("Unlink Store",
            "Remove the integration link? The store will remain but won't be connected to any integration.", "Unlink", "Cancel");
        if (!confirmed) return;

        var result = await _apiClient.UnlinkStoreLocationAsync(_store.Id);
        if (result.Success)
        {
            await LoadStoreAsync();
        }
        else
        {
            await DisplayAlert("Error", result.ErrorMessage ?? "Failed to unlink store", "OK");
        }
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadStoreAsync();
    }

    #endregion

    private void ShowLoading()
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = true;
        ErrorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        LoadingIndicator.IsVisible = false;
        LoadingIndicator.IsRunning = false;
        ContentScroll.IsVisible = false;
        ErrorFrame.IsVisible = true;
        ErrorLabel.Text = message;
    }
}
