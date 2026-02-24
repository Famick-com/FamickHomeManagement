using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages.Calendar;
using Famick.HomeManagement.Mobile.Pages.Wizard;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile.Pages;

public partial class DashboardPage : ContentPage
{
    private readonly ShoppingApiClient _apiClient;
    private readonly TokenStorage _tokenStorage;
    private readonly TenantStorage _tenantStorage;
    private readonly ConnectivityService _connectivityService;

    private ShoppingListDashboardDto? _shoppingDashboard;
    private StockStatisticsDto? _stockStatistics;
    private int _overdueChoresCount;
    private int _dueThisWeekCount;
    private List<CalendarOccurrence> _upcomingEvents = new();
    private bool _wizardRedirectAttempted;

    public DashboardPage(
        ShoppingApiClient apiClient,
        TokenStorage tokenStorage,
        TenantStorage tenantStorage,
        ConnectivityService connectivityService)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _tenantStorage = tenantStorage;
        _connectivityService = connectivityService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        _connectivityService.ConnectivityChanged += OnConnectivityChanged;

        // Check if logged in
        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
        {
            // Show login page modally
            var loginPage = Application.Current?.Handler?.MauiContext?.Services.GetService<LoginPage>();
            if (loginPage != null)
            {
                await Navigation.PushModalAsync(new NavigationPage(loginPage));
            }
            return;
        }

        // Set page title with tenant name
        Title = await _tenantStorage.GetAppTitleAsync();

        // Refresh Shell title (tenant name may have changed after login)
        if (Shell.Current is AppShell appShell)
        {
            await appShell.RefreshTitleAsync();
        }

        // Auto-redirect to wizard if not completed (only once per app session)
        var onboardingService = Application.Current?.Handler?.MauiContext?.Services.GetService<OnboardingService>();
        if (!_wizardRedirectAttempted && onboardingService != null && !onboardingService.IsHomeSetupWizardCompleted())
        {
            _wizardRedirectAttempted = true;
            // Check server wizard state to confirm it's actually incomplete
            var wizardResult = await _apiClient.GetWizardStateAsync();
            if (wizardResult.Success && wizardResult.Data != null && wizardResult.Data.IsComplete)
            {
                // Server says wizard is done - sync local state
                onboardingService.MarkHomeSetupWizardCompleted();
            }
            else if (wizardResult.Success)
            {
                // Wizard genuinely not complete - redirect
                var wizardPage = Application.Current?.Handler?.MauiContext?.Services.GetService<WizardHouseholdInfoPage>();
                if (wizardPage != null)
                {
                    await Navigation.PushAsync(wizardPage);
                    return;
                }
            }
            // If the API call failed (e.g. 404, no wizard endpoint), just skip and show dashboard
        }

        // Check if wizard needs to be shown (banner for re-run)
        CheckWizardBanner();

        // Check connectivity before loading data
        var isOnline = await _connectivityService.CheckServerReachableAsync();
        UpdateConnectivityUI(isOnline);

        if (isOnline)
        {
            await LoadDashboardWithRetryAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _connectivityService.ConnectivityChanged -= OnConnectivityChanged;
    }

    private async void OnConnectivityChanged(object? sender, bool isOnline)
    {
        UpdateConnectivityUI(isOnline);

        if (isOnline)
        {
            await LoadDashboardAsync();
        }
    }

    private void UpdateConnectivityUI(bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            OfflineBanner.IsVisible = !isOnline;
        });
    }

    private async Task LoadDashboardWithRetryAsync()
    {
        const int maxAttempts = 3;
        var delays = new[] { 1000, 2000, 4000 };

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            try
            {
                await LoadDashboardAsync();
                return; // Success
            }
            catch (HttpRequestException) when (attempt < maxAttempts - 1)
            {
                Console.WriteLine($"[Dashboard] Load attempt {attempt + 1} failed, retrying in {delays[attempt]}ms");
                await Task.Delay(delays[attempt]);
            }
        }
    }

    private void CheckWizardBanner()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var onboardingService = services?.GetService<OnboardingService>();
        if (onboardingService != null && !onboardingService.IsHomeSetupWizardCompleted())
        {
            MainThread.BeginInvokeOnMainThread(() => WizardBanner.IsVisible = true);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() => WizardBanner.IsVisible = false);
        }
    }

    private async Task LoadDashboardAsync()
    {
        ShowLoading(true);
        HideError();

        try
        {
            // Load all data in parallel
            await Task.WhenAll(
                LoadShoppingDashboardAsync(),
                LoadStockStatisticsAsync(),
                LoadChoresDashboardAsync(),
                LoadUpcomingEventsAsync()
            );

            UpdateUI();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load dashboard: {ex.Message}");
        }
        finally
        {
            ShowLoading(false);
        }
    }

    private async Task LoadShoppingDashboardAsync()
    {
        var result = await _apiClient.GetDashboardAsync();
        if (result.Success && result.Data != null)
        {
            _shoppingDashboard = result.Data;
        }
    }

    private async Task LoadStockStatisticsAsync()
    {
        var result = await _apiClient.GetStockStatisticsAsync();
        if (result.Success && result.Data != null)
        {
            _stockStatistics = result.Data;
        }
    }

    private async Task LoadChoresDashboardAsync()
    {
        // Get overdue chores
        var overdueResult = await _apiClient.GetOverdueChoresAsync();
        if (overdueResult.Success && overdueResult.Data != null)
        {
            _overdueChoresCount = overdueResult.Data.Count;
        }

        // Get chores due this week
        var dueSoonResult = await _apiClient.GetDueSoonChoresAsync(7);
        if (dueSoonResult.Success && dueSoonResult.Data != null)
        {
            _dueThisWeekCount = dueSoonResult.Data.Count;
        }
    }

    private void UpdateUI()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Shopping stats
            if (_shoppingDashboard != null)
            {
                ShoppingCountLabel.Text = _shoppingDashboard.UnpurchasedItems.ToString();
                ShoppingSubtitleLabel.Text = _shoppingDashboard.TotalLists == 1
                    ? "item in 1 list"
                    : $"items in {_shoppingDashboard.TotalLists} lists";
            }
            else
            {
                ShoppingCountLabel.Text = "0";
                ShoppingSubtitleLabel.Text = "items to buy";
            }

            // Stock alerts
            if (_stockStatistics != null)
            {
                LowStockCountLabel.Text = _stockStatistics.BelowMinStockCount.ToString();
                LowStockSubtitleLabel.Text = _stockStatistics.BelowMinStockCount == 1
                    ? "item low"
                    : "items low";

                // Expiring soon
                ExpiringCountLabel.Text = _stockStatistics.DueSoonCount.ToString();
                ExpiringSubtitleLabel.Text = _stockStatistics.DueSoonCount == 1
                    ? "item expiring"
                    : "items expiring";

                // Update colors based on counts
                if (_stockStatistics.BelowMinStockCount == 0)
                {
                    LowStockCountLabel.TextColor = Color.FromArgb("#4CAF50"); // Green
                    StockIconLabel.TextColor = Color.FromArgb("#4CAF50");
                }

                if (_stockStatistics.DueSoonCount == 0)
                {
                    ExpiringCountLabel.TextColor = Color.FromArgb("#4CAF50"); // Green
                }
            }
            else
            {
                LowStockCountLabel.Text = "0";
                LowStockSubtitleLabel.Text = "items low";
                ExpiringCountLabel.Text = "0";
                ExpiringSubtitleLabel.Text = "items expiring";
            }

            // Upcoming events
            RenderUpcomingEvents();

            // Chores
            var totalChoresDue = _overdueChoresCount + _dueThisWeekCount;
            ChoresCountLabel.Text = totalChoresDue.ToString();
            ChoresSubtitleLabel.Text = totalChoresDue == 1
                ? "due this week"
                : "due this week";

            // Update chores color based on overdue
            if (_overdueChoresCount > 0)
            {
                ChoresCountLabel.TextColor = Color.FromArgb("#E53935"); // Red
                ChoresIconLabel.TextColor = Color.FromArgb("#E53935");

                // Show overdue alert
                OverdueChoresAlert.IsVisible = true;
                OverdueChoresTitle.Text = _overdueChoresCount == 1
                    ? "1 Overdue Chore"
                    : $"{_overdueChoresCount} Overdue Chores";
                OverdueChoresSubtitle.Text = _overdueChoresCount == 1
                    ? "You have a chore that needs attention"
                    : "You have chores that need attention";
            }
            else
            {
                OverdueChoresAlert.IsVisible = false;
                if (totalChoresDue == 0)
                {
                    ChoresCountLabel.TextColor = Color.FromArgb("#4CAF50"); // Green
                }
            }
        });
    }

    private async Task LoadUpcomingEventsAsync()
    {
        try
        {
            var result = await _apiClient.GetUpcomingEventsAsync(7);
            if (result.Success && result.Data != null)
            {
                _upcomingEvents = result.Data.Take(5).ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Dashboard] Error loading upcoming events: {ex.Message}");
        }
    }

    private void RenderUpcomingEvents()
    {
        UpcomingEventsList.Children.Clear();
        UpcomingEventsSection.IsVisible = true;

        if (_upcomingEvents.Count == 0)
        {
            NoEventsLabel.IsVisible = true;
            return;
        }

        NoEventsLabel.IsVisible = false;

        foreach (var evt in _upcomingEvents)
        {
            var startLocal = evt.StartTimeUtc.ToLocalTime();
            var eventColor = !string.IsNullOrEmpty(evt.Color) ? evt.Color
                : (evt.IsExternal ? "#9E9E9E" : "#518751");

            var card = new Border
            {
                Padding = new Thickness(10, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Stroke = Colors.Transparent,
                BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Color.FromArgb("#333333") : Color.FromArgb("#F5F5F5")
            };

            var grid = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(new GridLength(3)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };

            // Color bar
            var colorBar = new BoxView
            {
                Color = SafeParseColor(eventColor),
                CornerRadius = 1,
                VerticalOptions = LayoutOptions.Fill
            };
            grid.Children.Add(colorBar);

            // Title
            var titleLabel = new Label
            {
                Text = evt.Title,
                FontSize = 14,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Application.Current?.RequestedTheme == AppTheme.Dark
                    ? Colors.White : Colors.Black
            };
            Grid.SetColumn(titleLabel, 1);
            grid.Children.Add(titleLabel);

            // Time
            var timeText = evt.IsAllDay ? "All Day"
                : startLocal.Date == DateTime.Today ? startLocal.ToString("h:mm tt")
                : startLocal.ToString("MMM d, h:mm tt");

            var timeLabel = new Label
            {
                Text = timeText,
                FontSize = 12,
                TextColor = Colors.Gray,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(timeLabel, 2);
            grid.Children.Add(timeLabel);

            card.Content = grid;

            // Tap to navigate
            if (!evt.IsExternal)
            {
                var tapGesture = new TapGestureRecognizer();
                var eventId = evt.EventId;
                var originalStart = evt.OriginalStartTimeUtc;
                tapGesture.Tapped += async (_, _) =>
                {
                    await Shell.Current.GoToAsync(nameof(CalendarEventDetailPage),
                        new Dictionary<string, object>
                        {
                            { "EventId", eventId.ToString() },
                            { "OriginalStartTimeUtc", originalStart?.ToString("O") ?? "" }
                        });
                };
                card.GestureRecognizers.Add(tapGesture);
            }

            UpcomingEventsList.Children.Add(card);
        }
    }

    private async void OnUpcomingEventsTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//CalendarPage");
    }

    private void ShowLoading(bool isLoading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = isLoading;
            LoadingIndicator.IsRunning = isLoading;
        });
    }

    private void ShowError(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorLabel.Text = message;
            ErrorAlert.IsVisible = true;
        });
    }

    private void HideError()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ErrorAlert.IsVisible = false;
        });
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadDashboardAsync();
        RefreshContainer.IsRefreshing = false;
    }

    private async void OnRetryClicked(object? sender, EventArgs e)
    {
        await LoadDashboardAsync();
    }

    private async void OnShoppingCardTapped(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ListSelectionPage");
    }

    private async void OnStockCardTapped(object? sender, EventArgs e)
    {
        // Stock management is not yet available in mobile - show info
        await DisplayAlertAsync(
            "Stock Management",
            "Stock management is currently only available in the web app. Low stock items should be added to your shopping list.",
            "OK");
    }

    private async void OnChoresCardTapped(object? sender, EventArgs e)
    {
        // Chores are not yet available in mobile - show info
        await DisplayAlertAsync(
            "Chores",
            "Chores management is currently only available in the web app.",
            "OK");
    }

    private async void OnExpiringCardTapped(object? sender, EventArgs e)
    {
        // Expiring items are not yet available in mobile - show info
        await DisplayAlertAsync(
            "Expiring Items",
            "Expiring items management is currently only available in the web app.",
            "OK");
    }

    private async void OnStartShoppingClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ListSelectionPage");
    }

    private async void OnViewListsClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//ListSelectionPage");
    }

    private async void OnQuickConsumeClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(QuickConsumePage));
    }

    private static Color SafeParseColor(string colorValue)
    {
        if (string.IsNullOrEmpty(colorValue))
            return Color.FromArgb("#518751");

        // Hex color
        if (colorValue[0] == '#')
        {
            try { return Color.FromArgb(colorValue); }
            catch { return Color.FromArgb("#518751"); }
        }

        // Named CSS color
        return colorValue.ToLowerInvariant() switch
        {
            "red" => Color.FromArgb("#F44336"),
            "blue" => Color.FromArgb("#2196F3"),
            "green" => Color.FromArgb("#4CAF50"),
            "yellow" => Color.FromArgb("#FFEB3B"),
            "orange" => Color.FromArgb("#FF9800"),
            "purple" => Color.FromArgb("#9C27B0"),
            "pink" => Color.FromArgb("#E91E63"),
            "teal" => Color.FromArgb("#009688"),
            "cyan" => Color.FromArgb("#00BCD4"),
            "brown" => Color.FromArgb("#795548"),
            "gray" or "grey" => Color.FromArgb("#9E9E9E"),
            "indigo" => Color.FromArgb("#3F51B5"),
            "amber" => Color.FromArgb("#FFC107"),
            "black" => Color.FromArgb("#000000"),
            "white" => Color.FromArgb("#FFFFFF"),
            _ => Color.FromArgb("#518751") // fallback green
        };
    }

    private async void OnWizardBannerTapped(object? sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var wizardPage = services?.GetService<WizardHouseholdInfoPage>();
        if (wizardPage != null)
        {
            await Navigation.PushAsync(wizardPage);
        }
    }
}
