using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;
using Famick.HomeManagement.Mobile.Popups;
using Famick.HomeManagement.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Famick.HomeManagement.Mobile.Pages;

// NOTE: This page builds its UI in code (BuildLayout) rather than XAML.
// A RefreshView > CollectionView with DataTemplate inside overlapping Grid rows
// in the original XAML caused an iOS MAUI rendering deadlock when the Shell
// navigated to this tab. Using a code-built layout with ScrollView +
// VerticalStackLayout avoids the issue. See commit history for the original XAML.

public partial class ListSelectionPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly LocationService _locationService;
    private readonly OfflineStorageService _offlineStorage;
    private readonly TokenStorage _tokenStorage;
    private bool _hasCheckedOnboarding;

    private ActivityIndicator _loadingIndicator = null!;
    private VerticalStackLayout _emptyState = null!;
    private ScrollView _listsScrollView = null!;
    private VerticalStackLayout _listsContainer = null!;
    private Border _errorFrame = null!;
    private Label _errorLabel = null!;
    private Button _retryButton = null!;
    private bool _isShowingLoginModal;
    private bool _isLoading;

    public ListSelectionPage(
        ShoppingApiClient apiClient,
        LocationService locationService,
        OfflineStorageService offlineStorage,
        TokenStorage tokenStorage)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _locationService = locationService;
        _offlineStorage = offlineStorage;
        _tokenStorage = tokenStorage;
        BuildLayout();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Delay the initial load slightly to let the Shell tab transition complete,
        // then reload every time the page appears (e.g. returning from a shopping session).
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), () =>
        {
            _ = HandleAppearingAsync();
        });
    }

    private async Task HandleAppearingAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(token))
        {
            if (_isShowingLoginModal) return;
            _isShowingLoginModal = true;

            var loginPage = Application.Current?.Handler?.MauiContext?.Services.GetService<LoginPage>();
            if (loginPage != null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Navigation.PushModalAsync(new NavigationPage(loginPage)));
            }

            _isShowingLoginModal = false;
            return;
        }

        // Check product onboarding on first visit
        if (!_hasCheckedOnboarding)
        {
            _hasCheckedOnboarding = true;
            await CheckProductOnboardingAsync().ConfigureAwait(false);
        }

        await LoadShoppingListsAsync().ConfigureAwait(false);
    }

    private async Task CheckProductOnboardingAsync()
    {
        try
        {
            var result = await _apiClient.GetProductOnboardingStateAsync().ConfigureAwait(false);

            System.Diagnostics.Debug.WriteLine(
                $"[ProductOnboarding] API result: Success={result.Success}, " +
                $"HasData={result.Data != null}, " +
                $"HasCompleted={result.Data?.HasCompletedOnboarding}, " +
                $"Error={result.ErrorMessage}");

            if (result.Success && result.Data != null && !result.Data.HasCompletedOnboarding)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var services = Application.Current?.Handler?.MauiContext?.Services;
                    var onboardingPage = services?.GetRequiredService<ProductOnboardingIntroPage>();
                    if (onboardingPage != null)
                    {
                        await Navigation.PushAsync(onboardingPage);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProductOnboarding] Check failed: {ex.Message}");
        }
    }

    private async Task LoadShoppingListsAsync()
    {
        if (_isLoading) return;

        _isLoading = true;
        MainThread.BeginInvokeOnMainThread(ShowLoading);

        try
        {
            var loadTask = _apiClient.GetShoppingListsAsync();
            var completedTask = await Task.WhenAny(loadTask, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            if (completedTask != loadTask)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError("Loading shopping lists timed out. Check the server connection and try again."));
                return;
            }

            var result = await loadTask.ConfigureAwait(false);

            if (result.Success && result.Data != null)
            {
                var localCounts = await _offlineStorage.GetAllLocalPurchaseCountsAsync().ConfigureAwait(false);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    _listsContainer.Children.Clear();
                    foreach (var list in result.Data)
                    {
                        if (localCounts.TryGetValue(list.Id, out var counts))
                        {
                            list.TotalItems = counts.total;
                            list.PurchasedItems = counts.purchased;
                        }
                        _listsContainer.Children.Add(CreateListCard(list));
                    }

                    if (_listsContainer.Children.Count == 0)
                        ShowEmpty();
                    else
                        ShowContent();
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    ShowError(result.ErrorMessage ?? "Failed to load shopping lists"));
            }
        }
        catch (Exception ex)
        {
            MainThread.BeginInvokeOnMainThread(() =>
                ShowError($"Connection error: {ex.Message}"));
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task StartShoppingSessionAsync(ShoppingListSummary list)
    {
        var detectedStore = await DetectNearbyStoreAsync().ConfigureAwait(false);

        if (detectedStore != null && detectedStore.Id != list.ShoppingLocationId)
        {
            var switchStore = await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlertAsync(
                    "Different Store Detected",
                    $"You appear to be at {detectedStore.Name}. Would you like to switch to this store?",
                    "Yes, Switch",
                    "No, Keep Original"));

            if (switchStore)
            {
                var request = new UpdateShoppingListRequest
                {
                    Name = list.Name,
                    ShoppingLocationId = detectedStore.Id
                };

                var result = await _apiClient.UpdateShoppingListAsync(list.Id, request).ConfigureAwait(false);
                if (result.Success)
                {
                    list.ShoppingLocationId = detectedStore.Id;
                    list.ShoppingLocationName = detectedStore.Name;
                }
            }
        }

        var navigationParameter = new Dictionary<string, object>
        {
            { "ListId", list.Id.ToString() },
            { "ListName", list.Name }
        };

        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync(nameof(ShoppingSessionPage), navigationParameter));
    }

    private async Task<StoreSummary?> DetectNearbyStoreAsync()
    {
        try
        {
            if (!await _locationService.IsLocationEnabledAsync().ConfigureAwait(false))
                return null;

            var location = await _locationService.GetCurrentLocationAsync().ConfigureAwait(false);
            if (location == null)
                return null;

            var storesResult = await _apiClient.GetShoppingLocationsAsync().ConfigureAwait(false);
            if (storesResult.Success && storesResult.Data != null)
            {
                return _locationService.FindNearestStore(storesResult.Data, location, maxDistanceMeters: 500);
            }
        }
        catch
        {
            // Location detection is best-effort
        }

        return null;
    }

    private void OnRefreshClicked(object? sender, EventArgs e)
    {
        _ = LoadShoppingListsAsync();
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _retryButton.IsEnabled = false;
            _retryButton.Text = "Retrying...";
        });

        try
        {
            await LoadShoppingListsAsync().ConfigureAwait(false);
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _retryButton.IsEnabled = true;
                _retryButton.Text = "Retry";
            });
        }
    }

    private async void OnServerSettingsClicked(object? sender, EventArgs e)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
            Shell.Current.GoToAsync(nameof(ServerConfigPage)));
    }

    private async void OnCreateListClicked(object? sender, EventArgs e)
    {
        var storesResult = await _apiClient.GetShoppingLocationsAsync();
        var stores = storesResult.Success && storesResult.Data != null
            ? storesResult.Data
            : new List<StoreSummary>();

        var popup = new CreateShoppingListPopup(stores, _apiClient);
        var popupResult = await this.ShowPopupAsync<CreateShoppingListResult>(popup, PopupOptions.Empty, CancellationToken.None);
        if (popupResult.WasDismissedByTappingOutsideOfPopup || popupResult.Result is null) return;

        var result = popupResult.Result;
        var createResult = await _apiClient.CreateShoppingListAsync(result.Name, result.Description, result.ShoppingLocationId);
        if (createResult.Success)
        {
            await LoadShoppingListsAsync();
        }
        else
        {
            await DisplayAlert("Error", createResult.ErrorMessage ?? "Failed to create shopping list", "OK");
        }
    }

    #region State Management

    private void ShowLoading()
    {
        _loadingIndicator.IsVisible = true;
        _loadingIndicator.IsRunning = true;
        _listsScrollView.IsVisible = false;
        _emptyState.IsVisible = false;
        _errorFrame.IsVisible = false;
    }

    private void ShowContent()
    {
        _loadingIndicator.IsVisible = false;
        _loadingIndicator.IsRunning = false;
        _listsScrollView.IsVisible = true;
        _emptyState.IsVisible = false;
        _errorFrame.IsVisible = false;
    }

    private void ShowEmpty()
    {
        _loadingIndicator.IsVisible = false;
        _loadingIndicator.IsRunning = false;
        _listsScrollView.IsVisible = false;
        _emptyState.IsVisible = true;
        _errorFrame.IsVisible = false;
    }

    private void ShowError(string message)
    {
        _loadingIndicator.IsVisible = false;
        _loadingIndicator.IsRunning = false;
        _listsScrollView.IsVisible = false;
        _emptyState.IsVisible = false;
        _errorFrame.IsVisible = true;
        _errorLabel.Text = message;
    }

    #endregion

    #region Layout Building

    private Border CreateListCard(ShoppingListSummary list)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ],
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            ]
        };

        grid.Add(new Label
        {
            Text = list.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        });

        var storeLabel = new Label
        {
            Text = list.ShoppingLocationName,
            FontSize = 14,
            TextColor = Colors.Gray
        };
        grid.Add(storeLabel);
        Grid.SetRow(storeLabel, 1);

        var countLabel = new Label
        {
            Text = list.ItemCountSummary,
            FontSize = 12,
            TextColor = Colors.Gray
        };
        grid.Add(countLabel);
        Grid.SetRow(countLabel, 2);

        var arrowLabel = new Label
        {
            Text = "\u203A",
            FontSize = 24,
            VerticalOptions = LayoutOptions.Center,
            TextColor = Colors.Gray
        };
        grid.Add(arrowLabel);
        Grid.SetColumn(arrowLabel, 1);
        Grid.SetRowSpan(arrowLabel, 3);

        var border = new Border
        {
            Margin = new Thickness(10, 5),
            Padding = 15,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Stroke = Colors.Transparent,
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2A2A2A")
                : Colors.White,
            Content = grid
        };

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (_, _) =>
        {
            await StartShoppingSessionAsync(list).ConfigureAwait(false);
        };
        border.GestureRecognizers.Add(tapGesture);

        return border;
    }

    private void BuildLayout()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var rootGrid = new Grid
        {
            BackgroundColor = isDark ? Color.FromArgb("#1C1C1E") : Colors.White,
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ]
        };

        // Header
        var header = new Grid
        {
            Padding = new Thickness(20, 15),
            BackgroundColor = isDark ? Color.FromArgb("#1A1A1A") : Color.FromArgb("#F5F5F5"),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };
        var headerText = new VerticalStackLayout { Spacing = 5 };
        headerText.Children.Add(new Label
        {
            Text = "Select a Shopping List",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        });
        headerText.Children.Add(new Label
        {
            Text = "Choose a list to start your shopping session",
            FontSize = 14,
            TextColor = Colors.Gray
        });
        header.Add(headerText);

        var addButton = new Button
        {
            Text = "+",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 22,
            Padding = 0,
            BackgroundColor = isDark ? Color.FromArgb("#1565C0") : Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        };
        addButton.Clicked += OnCreateListClicked;
        Grid.SetColumn(addButton, 1);
        header.Add(addButton);

        rootGrid.Add(header);

        // Loading indicator
        _loadingIndicator = new ActivityIndicator
        {
            IsRunning = true,
            IsVisible = true,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Color = isDark ? Color.FromArgb("#90CAF9") : Color.FromArgb("#1976D2")
        };
        Grid.SetRow(_loadingIndicator, 1);
        rootGrid.Add(_loadingIndicator);

        // Empty state
        _emptyState = new VerticalStackLayout
        {
            IsVisible = false,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 10
        };
        _emptyState.Children.Add(new Label
        {
            Text = "No Shopping Lists",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        });
        _emptyState.Children.Add(new Label
        {
            Text = "Tap + to create your first shopping list.",
            FontSize = 14,
            TextColor = Colors.Gray,
            HorizontalOptions = LayoutOptions.Center
        });
        var refreshButton = new Button
        {
            Text = "Refresh",
            Margin = new Thickness(0, 20, 0, 0)
        };
        refreshButton.Clicked += OnRefreshClicked;
        _emptyState.Children.Add(refreshButton);
        Grid.SetRow(_emptyState, 1);
        rootGrid.Add(_emptyState);

        // Shopping lists container
        _listsContainer = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(0, 5)
        };
        _listsScrollView = new ScrollView
        {
            IsVisible = false,
            Content = _listsContainer
        };
        Grid.SetRow(_listsScrollView, 1);
        rootGrid.Add(_listsScrollView);

        // Error state
        _errorLabel = new Label
        {
            TextColor = Color.FromArgb("#C62828"),
            HorizontalOptions = LayoutOptions.Center
        };
        _retryButton = new Button { Text = "Retry" };
        _retryButton.Clicked += OnRetryClicked;

        var settingsButton = new Button
        {
            Text = "Server Settings",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#1976D2")
        };
        settingsButton.Clicked += OnServerSettingsClicked;

        _errorFrame = new Border
        {
            IsVisible = false,
            Margin = 20,
            Padding = 20,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Stroke = Colors.Transparent,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { _errorLabel, _retryButton, settingsButton }
            }
        };
        Grid.SetRow(_errorFrame, 1);
        rootGrid.Add(_errorFrame);

        RootHost.Children.Clear();
        RootHost.Children.Add(rootGrid);
    }

    #endregion
}
