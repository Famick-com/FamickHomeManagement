using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Pages;
using Famick.HomeManagement.Mobile.Pages.Onboarding;
using Famick.HomeManagement.Mobile.Pages.StorageBins;
using Famick.HomeManagement.Mobile.Services;

namespace Famick.HomeManagement.Mobile;

public partial class App : Application
{
    private readonly OnboardingService _onboardingService;
    private readonly TokenStorage _tokenStorage;
    private readonly ApiSettings _apiSettings;
    private bool _isShowingLogin;

    /// <summary>
    /// Pending deep link to process when the app is ready
    /// </summary>
    public static DeepLinkInfo? PendingDeepLink { get; set; }

    /// <summary>
    /// Pending verification token from email deep link
    /// </summary>
    public static string? PendingVerificationToken { get; set; }

    /// <summary>
    /// Pending quick consume action from shortcut or widget
    /// </summary>
    public static bool PendingQuickConsume { get; set; }

    /// <summary>
    /// Pending storage bin short code from deep link
    /// </summary>
    public static string? PendingStorageBinShortCode { get; set; }

    public App(OnboardingService onboardingService, TokenStorage tokenStorage, ApiSettings apiSettings)
    {
        InitializeComponent();
        _onboardingService = onboardingService;
        _tokenStorage = tokenStorage;
        _apiSettings = apiSettings;

        WeakReferenceMessenger.Default.Register<SessionExpiredMessage>(this, (_, msg) =>
        {
            Console.WriteLine($"[App] SessionExpired: {msg.Value}");
            MainThread.BeginInvokeOnMainThread(async () => await ShowLoginForSessionExpiredAsync());
        });
    }

    private async Task ShowLoginForSessionExpiredAsync()
    {
        if (_isShowingLogin) return;
        _isShowingLogin = true;

        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var loginPage = services?.GetService<LoginPage>();
            if (loginPage != null && Current?.MainPage != null)
            {
                var navPage = new NavigationPage(loginPage);
                navPage.Popped += (_, _) =>
                {
                    // Only reset when the login modal navigation is fully dismissed
                    if (navPage.Navigation.NavigationStack.Count <= 1)
                        _isShowingLogin = false;
                };
                await Current.MainPage.Navigation.PushModalAsync(navPage);
                // Don't reset _isShowingLogin here — it stays true until the modal is dismissed
                // to prevent multiple session-expired modals from stacking
            }
            else
            {
                _isShowingLogin = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] ShowLoginForSessionExpired error: {ex.Message}");
            _isShowingLogin = false;
        }
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Determine the initial state based on onboarding/authentication status
        var state = _onboardingService.GetCurrentState(_tokenStorage, _apiSettings);

        Page startPage = state switch
        {
            OnboardingState.Welcome => CreateOnboardingNavigationPage(),
            OnboardingState.EmailVerification => CreateEmailVerificationPage(),
            OnboardingState.Login => new AppShell(),
            OnboardingState.HomeSetupWizard => new AppShell(),
            OnboardingState.LoggedIn => new AppShell(),
            _ => CreateOnboardingNavigationPage()
        };

        var window = new Window(startPage);

        // Handle pending deep links after window is created
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await Task.Delay(500); // Give page time to initialize

            // Handle verification token if present
            if (!string.IsNullOrEmpty(PendingVerificationToken))
            {
                await ProcessPendingVerificationTokenAsync();
            }
            // Handle quick consume deep link if present
            else if (PendingQuickConsume)
            {
                await ProcessPendingQuickConsumeAsync();
            }
            // Handle storage bin deep link if present
            else if (!string.IsNullOrEmpty(PendingStorageBinShortCode))
            {
                await ProcessPendingStorageBinDeepLinkAsync();
            }
            // Handle shopping deep link if present
            else if (PendingDeepLink != null)
            {
                await ProcessPendingDeepLinkAsync();
            }
        });

        return window;
    }

    private NavigationPage CreateOnboardingNavigationPage()
    {
        var services = Handler?.MauiContext?.Services;
        if (services == null)
        {
            // Fallback - create with properly configured dependencies
            var innerHandler = new DynamicApiHttpHandler(_apiSettings);
            var authHandler = new AuthenticatingHttpHandler(_tokenStorage, _apiSettings)
            {
                InnerHandler = innerHandler
            };
            var httpClient = new HttpClient(authHandler)
            {
                BaseAddress = new Uri(_apiSettings.BaseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };
            return new NavigationPage(new WelcomePage(
                new ShoppingApiClient(httpClient),
                new OnboardingService()));
        }

        var welcomePage = services.GetRequiredService<WelcomePage>();
        return new NavigationPage(welcomePage);
    }

    private NavigationPage CreateEmailVerificationPage()
    {
        var email = _onboardingService.GetPendingVerificationEmail() ?? "";
        var householdName = ""; // TODO: Store household name in preferences if needed

        var services = Handler?.MauiContext?.Services;
        if (services == null)
        {
            return CreateOnboardingNavigationPage();
        }

        var apiClient = services.GetRequiredService<ShoppingApiClient>();
        var verificationPage = new EmailVerificationPage(apiClient, _onboardingService, email, householdName);
        return new NavigationPage(verificationPage);
    }

    protected override void OnResume()
    {
        base.OnResume();

        // Resume BLE scanner connection if disconnected
        var bleService = Handler?.MauiContext?.Services.GetService<BleScannerService>();
        if (bleService is { HasSavedScanner: true, IsConnected: false })
            _ = bleService.ResumeConnectionAsync();

        // Auto-sync contacts in background on resume
        _ = SyncContactsInBackgroundAsync();

        // Check for pending deep links when app resumes
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!string.IsNullOrEmpty(PendingVerificationToken))
            {
                await ProcessPendingVerificationTokenAsync();
            }
            else if (PendingQuickConsume)
            {
                await ProcessPendingQuickConsumeAsync();
            }
            else if (!string.IsNullOrEmpty(PendingStorageBinShortCode))
            {
                await ProcessPendingStorageBinDeepLinkAsync();
            }
            else if (PendingDeepLink != null)
            {
                await ProcessPendingDeepLinkAsync();
            }
        });
    }

    protected override void OnSleep()
    {
        base.OnSleep();

        // Stop BLE scanner reconnection attempts in background to save battery
        var bleService = Handler?.MauiContext?.Services.GetService<BleScannerService>();
        bleService?.StopReconnecting();

        // Refresh widget data when app goes to background
        // This ensures widgets show current data even if user hasn't consumed recently
        _ = RefreshWidgetDataInBackgroundAsync();
    }

    /// <summary>
    /// Auto-sync contacts in background if enough time has elapsed since last sync.
    /// </summary>
    private static async Task SyncContactsInBackgroundAsync()
    {
        try
        {
            if (!ContactSyncOrchestrator.ShouldSync(TimeSpan.FromHours(1)))
                return;

            var orchestrator = Current?.Handler?.MauiContext?.Services.GetService<ContactSyncOrchestrator>();
            if (orchestrator == null) return;

            await orchestrator.SyncAsync();
        }
        catch (Exception ex)
        {
            // Swallow — background sync is non-critical
            Console.WriteLine($"[App] Background contact sync failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Refresh widget data in background - used when app goes to sleep or after login
    /// </summary>
    private static async Task RefreshWidgetDataInBackgroundAsync()
    {
        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();

            if (apiClient != null)
            {
                await apiClient.RefreshWidgetDataAsync();
                Console.WriteLine("[App] Widget data refreshed in background");
            }
        }
        catch (Exception ex)
        {
            // Swallow errors - widget refresh is not critical
            Console.WriteLine($"[App] Widget data refresh failed: {ex.Message}");
        }
    }

    private static async Task ProcessPendingStorageBinDeepLinkAsync()
    {
        if (string.IsNullOrEmpty(PendingStorageBinShortCode)) return;

        var shortCode = PendingStorageBinShortCode;
        PendingStorageBinShortCode = null;

        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();
            if (apiClient == null) return;

            var result = await apiClient.GetStorageBinByCodeAsync(shortCode);
            if (result.Success && result.Data != null)
            {
                await Shell.Current.GoToAsync(nameof(StorageBinDetailPage),
                    new Dictionary<string, object> { ["StorageBinId"] = result.Data.Id.ToString() });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process storage bin deep link: {ex.Message}");
        }
    }

    private static async Task ProcessPendingDeepLinkAsync()
    {
        if (PendingDeepLink == null) return;

        var deepLink = PendingDeepLink;
        PendingDeepLink = null; // Clear to avoid re-processing

        try
        {
            // Navigate to the shopping session page with the list ID
            var navigationParameter = new Dictionary<string, object>
            {
                { "ListId", deepLink.ListId.ToString() },
                { "ListName", deepLink.ListName }
            };

            await Shell.Current.GoToAsync(nameof(ShoppingSessionPage), navigationParameter);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process deep link: {ex.Message}");
        }
    }

    /// <summary>
    /// Process pending quick consume action - navigates to QuickConsumePage
    /// </summary>
    private static async Task ProcessPendingQuickConsumeAsync()
    {
        if (!PendingQuickConsume) return;

        PendingQuickConsume = false; // Clear to avoid re-processing

        try
        {
            await Shell.Current.GoToAsync(nameof(QuickConsumePage));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process quick consume: {ex.Message}");
        }
    }

    private async Task ProcessPendingVerificationTokenAsync()
    {
        if (string.IsNullOrEmpty(PendingVerificationToken)) return;

        var token = PendingVerificationToken;
        PendingVerificationToken = null; // Clear to avoid re-processing

        try
        {
            // Store the token for the verification page to use
            _onboardingService.SetPendingVerification(
                _onboardingService.GetPendingVerificationEmail() ?? "",
                token);

            // If we're on the email verification page, it will pick up the token
            // Otherwise, navigate to it
            if (Current?.MainPage is NavigationPage navPage)
            {
                if (navPage.CurrentPage is EmailVerificationPage verificationPage)
                {
                    // Page will handle it in OnAppearing
                    verificationPage.HandleVerificationToken(token);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process verification token: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles deep link from iOS/Android
    /// </summary>
    public static void HandleDeepLink(Uri uri)
    {
        if (uri == null) return;

        var query = ParseQueryString(uri.Query);

        // Handle setup deep link: famick://setup?url=...&name=...
        if (uri.Host == "setup" || uri.AbsolutePath.Contains("setup"))
        {
            var serverUrl = query.GetValueOrDefault("url");
            var serverName = query.GetValueOrDefault("name");

            if (!string.IsNullOrEmpty(serverUrl))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ProcessSetupDeepLinkAsync(serverUrl, serverName);
                });
            }
            return;
        }

        // Handle quick consume deep link: famick://quick-consume
        if (uri.Host == "quick-consume" || uri.AbsolutePath.Contains("quick-consume"))
        {
            PendingQuickConsume = true;

            // If the app is already running, process immediately
            if (Current?.MainPage != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ProcessPendingQuickConsumeAsync();
                });
            }
            return;
        }

        // Handle verification deep link: famick://verify?token=...
        if (uri.Host == "verify" || uri.AbsolutePath.Contains("verify"))
        {
            var token = query.GetValueOrDefault("token");
            if (!string.IsNullOrEmpty(token))
            {
                PendingVerificationToken = token;

                // If the app is already running, process immediately
                if (Current?.MainPage != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        if (Current is App app)
                        {
                            await app.ProcessPendingVerificationTokenAsync();
                        }
                    });
                }
            }
            return;
        }

        // Handle storage bin deep link: https://app.famick.com/storage/{tenantId}/{shortCode}
        if (uri.AbsolutePath.StartsWith("/storage/"))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 3)
            {
                var shortCode = segments[2];
                PendingStorageBinShortCode = shortCode;

                if (Current?.MainPage != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await ProcessPendingStorageBinDeepLinkAsync();
                    });
                }
            }
            return;
        }

        // Handle shopping deep link: famickshopping://shopping/session?ListId={guid}&ListName={name}
        var listId = query.GetValueOrDefault("ListId");
        var listName = query.GetValueOrDefault("ListName");

        if (!string.IsNullOrEmpty(listId) && Guid.TryParse(listId, out var parsedListId))
        {
            PendingDeepLink = new DeepLinkInfo
            {
                ListId = parsedListId,
                ListName = listName ?? "Shopping"
            };

            // If the app is already running, process immediately
            if (Current?.MainPage != null)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await ProcessPendingDeepLinkAsync();
                });
            }
        }
    }

    /// <summary>
    /// Process setup deep link - configures server and navigates to login
    /// </summary>
    private static async Task ProcessSetupDeepLinkAsync(string serverUrl, string? serverName)
    {
        try
        {
            Console.WriteLine($"[App.ProcessSetupDeepLinkAsync] Processing setup: url={serverUrl}, name={serverName}");

            var services = Current?.Handler?.MauiContext?.Services;
            if (services == null)
            {
                Console.WriteLine("[App.ProcessSetupDeepLinkAsync] Services not available");
                return;
            }

            var apiSettings = services.GetRequiredService<Services.ApiSettings>();
            var apiClient = services.GetRequiredService<Services.ShoppingApiClient>();
            var onboardingService = services.GetRequiredService<Services.OnboardingService>();

            // Configure the server
            apiSettings.ConfigureFromQrCode(serverUrl, serverName);

            // Test connection
            var isHealthy = await apiClient.CheckHealthAsync();
            Console.WriteLine($"[App.ProcessSetupDeepLinkAsync] Health check: {isHealthy}");

            if (isHealthy)
            {
                // Mark onboarding as complete and transition to login
                onboardingService.MarkOnboardingCompleted();
                TransitionToMainApp();
            }
            else
            {
                // Show error - server not reachable
                if (Current?.MainPage != null)
                {
                    await Current.MainPage.DisplayAlert(
                        "Connection Failed",
                        $"Could not connect to server at {serverUrl}. Please check the URL and try again.",
                        "OK");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App.ProcessSetupDeepLinkAsync] Error: {ex.Message}");
            if (Current?.MainPage != null)
            {
                await Current.MainPage.DisplayAlert(
                    "Setup Error",
                    $"Failed to configure server: {ex.Message}",
                    "OK");
            }
        }
    }

    /// <summary>
    /// Parse query string with proper URL decoding (handles both %20 and + as space)
    /// </summary>
    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
            return result;

        if (query.StartsWith("?"))
            query = query[1..];

        foreach (var pair in query.Split('&'))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2)
            {
                // Replace + with space before URL decoding (standard form encoding)
                var key = Uri.UnescapeDataString(parts[0].Replace('+', ' '));
                var value = Uri.UnescapeDataString(parts[1].Replace('+', ' '));
                result[key] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Transitions from onboarding to the main app shell
    /// </summary>
    public static void TransitionToMainApp()
    {
        Console.WriteLine("[App.TransitionToMainApp] Called");
        if (Current == null)
        {
            Console.WriteLine("[App.TransitionToMainApp] Current is null, returning");
            return;
        }

        Console.WriteLine("[App.TransitionToMainApp] Scheduling MainPage change on main thread");
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                Console.WriteLine("[App.TransitionToMainApp] Setting MainPage to AppShell");
                Current.MainPage = new AppShell();
                Console.WriteLine("[App.TransitionToMainApp] MainPage set successfully");

                // Refresh widget data after login/transition
                await RefreshWidgetDataInBackgroundAsync();

                // Auto-sync contacts after login/transition
                _ = SyncContactsInBackgroundAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App.TransitionToMainApp] Error: {ex.Message}");
                Console.WriteLine($"[App.TransitionToMainApp] Stack: {ex.StackTrace}");
            }
        });
    }
}

/// <summary>
/// Information about a deep link to process
/// </summary>
public class DeepLinkInfo
{
    public Guid ListId { get; set; }
    public string ListName { get; set; } = string.Empty;
}
