using Famick.HomeManagement.Mobile.Pages;
using Famick.HomeManagement.Mobile.Pages.Calendar;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Pages.Recipes;
using Famick.HomeManagement.Mobile.Pages.Wizard;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile;

public partial class AppShell : Shell
{
    private string _appTitle = "Famick Home";
    private ToolbarItem _notificationBellToolbarItem = null!;
    private bool _hasUnreadNotifications;

    public AppShell()
    {
        InitializeComponent();

        _notificationBellToolbarItem = new ToolbarItem
        {
            Text = "Notifications",
            IconImageSource = "notification_bell",
            Order = ToolbarItemOrder.Primary,
            AutomationId = "NotificationBellToolbarItem"
        };
        _notificationBellToolbarItem.Clicked += OnNotificationBellClicked;
        ToolbarItems.Add(_notificationBellToolbarItem);

        // Register routes for navigation (LoginPage is shown modally, not via Shell routing)
        Routing.RegisterRoute(nameof(ServerConfigPage), typeof(ServerConfigPage));
        Routing.RegisterRoute(nameof(ShoppingSessionPage), typeof(ShoppingSessionPage));
        Routing.RegisterRoute(nameof(AddItemPage), typeof(AddItemPage));
        Routing.RegisterRoute(nameof(BarcodeScannerPage), typeof(BarcodeScannerPage));
        Routing.RegisterRoute(nameof(NotificationsPage), typeof(NotificationsPage));
        Routing.RegisterRoute(nameof(NotificationSettingsPage), typeof(NotificationSettingsPage));
        Routing.RegisterRoute(nameof(BarcodeScannerSettingsPage), typeof(BarcodeScannerSettingsPage));
        Routing.RegisterRoute(nameof(AisleOrderPage), typeof(AisleOrderPage));
        Routing.RegisterRoute(nameof(QuickConsumePage), typeof(QuickConsumePage));
        Routing.RegisterRoute(nameof(ChildProductSelectionPage), typeof(ChildProductSelectionPage));

        // Calendar routes
        Routing.RegisterRoute(nameof(CalendarEventDetailPage), typeof(CalendarEventDetailPage));
        Routing.RegisterRoute(nameof(CreateEditEventPage), typeof(CreateEditEventPage));

        // Recipe routes
        Routing.RegisterRoute(nameof(RecipeDetailPage), typeof(RecipeDetailPage));
        Routing.RegisterRoute(nameof(RecipeEditPage), typeof(RecipeEditPage));
        Routing.RegisterRoute(nameof(RecipeStepsPage), typeof(RecipeStepsPage));
        Routing.RegisterRoute(nameof(AddIngredientPage), typeof(AddIngredientPage));

        // Contact routes
        Routing.RegisterRoute(nameof(ContactGroupDetailPage), typeof(ContactGroupDetailPage));
        Routing.RegisterRoute(nameof(ContactGroupEditPage), typeof(ContactGroupEditPage));
        Routing.RegisterRoute(nameof(ContactDetailPage), typeof(ContactDetailPage));
        Routing.RegisterRoute(nameof(ContactEditPage), typeof(ContactEditPage));
        Routing.RegisterRoute(nameof(ContactAuditLogPage), typeof(ContactAuditLogPage));
        Routing.RegisterRoute(nameof(ContactTagsPage), typeof(ContactTagsPage));

        // Wizard routes
        Routing.RegisterRoute(nameof(WizardHouseholdInfoPage), typeof(WizardHouseholdInfoPage));
        Routing.RegisterRoute(nameof(WizardMembersPage), typeof(WizardMembersPage));
        Routing.RegisterRoute(nameof(WizardHomeStatsPage), typeof(WizardHomeStatsPage));
        Routing.RegisterRoute(nameof(WizardMaintenancePage), typeof(WizardMaintenancePage));
        Routing.RegisterRoute(nameof(WizardVehiclesPage), typeof(WizardVehiclesPage));
        Routing.RegisterRoute(nameof(WizardVehicleEditPage), typeof(WizardVehicleEditPage));

        // Load tenant name and update title
        _ = LoadTenantNameAsync();

        // Start periodic health checks for connectivity monitoring
        _ = StartHealthChecksAsync();

        // Auto-connect BLE scanner if previously paired
        _ = AutoConnectBleScannerAsync();

        // Start polling for unread notification count
        _ = StartNotificationPollingAsync();

        // Register for push notifications
        _ = RegisterPushNotificationsAsync();
    }

    private async Task StartHealthChecksAsync()
    {
        try
        {
            ConnectivityService? connectivityService = null;
            for (int i = 0; i < 10 && connectivityService == null; i++)
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                connectivityService = services?.GetService<ConnectivityService>();
                if (connectivityService == null)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            connectivityService?.StartHealthChecks(TimeSpan.FromSeconds(30));
            Console.WriteLine("[AppShell] Health checks started");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] StartHealthChecksAsync error: {ex.Message}");
        }
    }

    private async Task AutoConnectBleScannerAsync()
    {
        try
        {
            BleScannerService? bleService = null;
            for (int i = 0; i < 10 && bleService == null; i++)
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                bleService = services?.GetService<BleScannerService>();
                if (bleService == null)
                    await Task.Delay(100).ConfigureAwait(false);
            }

            if (bleService is { HasSavedScanner: true })
            {
                await bleService.InitializeAsync();
                await bleService.AutoConnectAsync();
                Console.WriteLine($"[AppShell] BLE scanner auto-connect: {bleService.ConnectionState}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] AutoConnectBleScannerAsync error: {ex.Message}");
        }
    }

    private async Task RegisterPushNotificationsAsync()
    {
        try
        {
            PushNotificationRegistrationService? pushService = null;
            for (int i = 0; i < 10 && pushService == null; i++)
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                pushService = services?.GetService<PushNotificationRegistrationService>();
                if (pushService == null)
                    await Task.Delay(100).ConfigureAwait(false);
            }

            if (pushService != null)
            {
                await pushService.RegisterAsync().ConfigureAwait(false);
                Console.WriteLine("[AppShell] Push notification registration completed");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] RegisterPushNotificationsAsync error: {ex.Message}");
        }
    }

    private async Task LoadTenantNameAsync()
    {
        try
        {
            // Wait for MauiContext to be available (may not be ready in constructor)
            TenantStorage? tenantStorage = null;
            ApiSettings? apiSettings = null;
            for (int i = 0; i < 10 && tenantStorage == null; i++)
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                tenantStorage = services?.GetService<TenantStorage>();
                apiSettings = services?.GetService<ApiSettings>();
                if (tenantStorage == null)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            if (tenantStorage != null)
            {
                _appTitle = await tenantStorage.GetAppTitleAsync().ConfigureAwait(false);
                var tenantName = apiSettings?.TenantName ?? await tenantStorage.GetTenantNameAsync().ConfigureAwait(false);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Title = _appTitle;

                    // Update household name in navigation bar title
                    if (!string.IsNullOrEmpty(tenantName))
                    {
                        TitleLabel.Text = tenantName;
                        HouseholdNameLabel.Text = tenantName;
                    }
                    else
                    {
                        TitleLabel.Text = "Famick Home";
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] LoadTenantNameAsync error: {ex.Message}");
            // Ignore errors loading tenant name
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        FlyoutIsPresented = false;

        var services = Application.Current?.Handler?.MauiContext?.Services;
        var settingsPage = services?.GetService<SettingsPage>();
        if (settingsPage != null)
        {
            await Current.Navigation.PushAsync(settingsPage);
        }
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        FlyoutIsPresented = false;

        var confirm = await DisplayAlertAsync("Sign Out", "Are you sure you want to sign out?", "Yes", "Cancel");
        if (!confirm) return;

        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services == null) return;

        // Unregister push notification device token before clearing auth
        try
        {
            var pushService = services.GetService<PushNotificationRegistrationService>();
            if (pushService != null)
            {
                await pushService.UnregisterAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] Push unregister on sign-out error: {ex.Message}");
        }

        // Clear tokens
        var tokenStorage = services.GetService<TokenStorage>();
        if (tokenStorage != null)
        {
            await tokenStorage.ClearTokensAsync();
        }

        // Clear tenant name display (but keep server configuration)
        var tenantStorage = services.GetService<TenantStorage>();
        tenantStorage?.Clear();

        // Reset title to default
        _appTitle = "Famick Home";
        Title = _appTitle;

        // Navigate to login page (server is still configured, so they can log back in)
        var loginPage = services.GetService<LoginPage>();
        if (loginPage != null)
        {
            await Navigation.PushModalAsync(new NavigationPage(loginPage));
        }
    }

    private async Task StartNotificationPollingAsync()
    {
        try
        {
            // Wait for services to be available
            ShoppingApiClient? apiClient = null;
            for (int i = 0; i < 10 && apiClient == null; i++)
            {
                var services = Application.Current?.Handler?.MauiContext?.Services;
                apiClient = services?.GetService<ShoppingApiClient>();
                if (apiClient == null)
                    await Task.Delay(100).ConfigureAwait(false);
            }

            if (apiClient == null) return;

            // Poll every 60 seconds
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));

            // Initial check
            await UpdateNotificationBadgeAsync(apiClient).ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync().ConfigureAwait(false))
            {
                await UpdateNotificationBadgeAsync(apiClient).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] NotificationPolling error: {ex.Message}");
        }
    }

    private async Task UpdateNotificationBadgeAsync(ShoppingApiClient apiClient)
    {
        try
        {
            var result = await apiClient.GetUnreadNotificationCountAsync().ConfigureAwait(false);
            if (result.Success && result.Data != null)
            {
                var hasUnread = result.Data.Count > 0;
                if (hasUnread != _hasUnreadNotifications)
                {
                    _hasUnreadNotifications = hasUnread;
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        _notificationBellToolbarItem.IconImageSource = hasUnread
                            ? "notification_bell_unread"
                            : "notification_bell";
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] UpdateNotificationBadge error: {ex.Message}");
        }
    }

    private async void OnNotificationBellClicked(object? sender, EventArgs e)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        var page = services?.GetService<NotificationsPage>();
        if (page != null)
        {
            await Current.Navigation.PushAsync(page);
        }
    }

    /// <summary>
    /// Refreshes the app title from tenant storage. Call this after login.
    /// </summary>
    public async Task RefreshTitleAsync()
    {
        await LoadTenantNameAsync().ConfigureAwait(false);
    }
}
