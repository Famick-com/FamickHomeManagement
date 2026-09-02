using CommunityToolkit.Mvvm.Messaging;
using Famick.HomeManagement.Mobile.Messages;
using Famick.HomeManagement.Mobile.Models;
using Famick.HomeManagement.Mobile.Pages;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Pages.Onboarding;
using Famick.HomeManagement.Mobile.Pages.StorageBins;
using Famick.HomeManagement.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Mobile;

public partial class App : Application
{
    private readonly OnboardingService _onboardingService;
    private readonly TokenStorage _tokenStorage;
    private readonly ApiSettings _apiSettings;
    private bool _isShowingLogin;
    private bool _isShowingForceChangePassword;
    private bool _isShowingAcceptTerms;
    private bool _isShowingStepUp;

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

    /// <summary>
    /// Pending shared contact data from share intent or vCard file
    /// </summary>
    public static SharedContactData? PendingSharedContact { get; set; }

    public App(OnboardingService onboardingService, TokenStorage tokenStorage, ApiSettings apiSettings, MessageBusAdapter messageBusAdapter)
    {
        InitializeComponent();
        _onboardingService = onboardingService;
        _tokenStorage = tokenStorage;
        _apiSettings = apiSettings;
        _ = messageBusAdapter; // Resolve to activate bridging

        // Syncfusion's theme has no automatic setting — it is one of four fixed values — so it
        // has to be pointed at the right one now and repointed whenever the system theme
        // changes. Without this its controls stay light while the rest of the app goes dark.
        ApplySyncfusionTheme();
        RequestedThemeChanged += (_, _) => ApplySyncfusionTheme();

        WeakReferenceMessenger.Default.Register<SessionExpiredMessage>(this, (_, msg) =>
        {
            Console.WriteLine($"[App] SessionExpired: {msg.Value}");
            MainThread.BeginInvokeOnMainThread(async () => await ShowLoginForSessionExpiredAsync());
        });

        WeakReferenceMessenger.Default.Register<MustChangePasswordMessage>(this, (_, msg) =>
        {
            Console.WriteLine($"[App] MustChangePassword: {msg.Value}");
            MainThread.BeginInvokeOnMainThread(async () => await ShowForceChangePasswordAsync());
        });

        WeakReferenceMessenger.Default.Register<MustAcceptTermsMessage>(this, (_, msg) =>
        {
            Console.WriteLine($"[App] MustAcceptTerms: {msg.Value}");
            MainThread.BeginInvokeOnMainThread(async () => await ShowAcceptTermsAsync());
        });

        // Phase 2.5 — step-up re-auth modal. The message carries the TCS that
        // AuthenticatingHttpHandler is awaiting; the modal completes it on
        // submit (with new access token) or cancel (with null).
        WeakReferenceMessenger.Default.Register<StepUpRequiredMessage>(this, (_, msg) =>
        {
            Console.WriteLine("[App] StepUpRequired received");
            MainThread.BeginInvokeOnMainThread(async () => await ShowStepUpReauthAsync(msg.Value));
        });

        // Phase 4 chunk 4.G originally broadcast LocalServerChangedMessage
        // here and pushed the prompt modally. That raced the login modal's
        // pop/transition on iOS — see follow-up plan. The detector now
        // returns the payload directly to each login site so the prompt can
        // be pushed via Navigation.PushAsync inline. ShowLocalServerChangePrompt
        // below survives as the UL/AL OAuth resume entry point, which is
        // the only path that goes through App.xaml.cs rather than a page.
    }

    /// <summary>
    /// Phase 4 follow-up — push the change-prompt for the UL/AL OAuth
    /// resume path (the only OAuth path that completes in App.xaml.cs
    /// rather than on a Page). Other login sites push directly on their
    /// own Navigation stack.
    /// </summary>
    private static async Task ShowLocalServerChangePromptAsync(Messages.LocalServerChangedPayload payload)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services;
        if (services is null) return;
        var tokenStorage = services.GetRequiredService<TokenStorage>();
        var nav = Application.Current?.Windows[0]?.Page?.Navigation;
        if (nav is null)
        {
            // No nav context available — silently persist the new URL so the
            // user isn't stuck re-prompted on the next login. Trade-off vs
            // showing nothing at all: accept the new URL without explicit
            // confirmation in this edge case (deep link before any page is
            // mounted). Acceptable since UL/AL is already an OS-verified
            // domain handoff.
            Preferences.Default.Set(Services.LocalServerChangeDetector.LastLocalServerKey, payload.NewUrl);
            return;
        }
        var page = new Pages.LocalServerChangePromptPage(tokenStorage, payload.OldUrl, payload.NewUrl);
        await nav.PushAsync(page);
    }

    private async Task ShowLoginForSessionExpiredAsync()
    {
        if (_isShowingLogin) return;

        // DashboardPage already showing a login modal -- don't stack another
        if (Current?.MainPage?.Navigation.ModalStack.Count > 0) return;

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

    private async Task ShowForceChangePasswordAsync()
    {
        if (_isShowingForceChangePassword) return;
        _isShowingForceChangePassword = true;

        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var forcePage = services?.GetService<ForceChangePasswordPage>();
            if (forcePage != null && Current?.MainPage != null)
            {
                var navPage = new NavigationPage(forcePage);
                navPage.Popped += (_, _) =>
                {
                    if (navPage.Navigation.NavigationStack.Count <= 1)
                        _isShowingForceChangePassword = false;
                };
                await Current.MainPage.Navigation.PushModalAsync(navPage);
            }
            else
            {
                _isShowingForceChangePassword = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] ShowForceChangePassword error: {ex.Message}");
            _isShowingForceChangePassword = false;
        }
    }

    private async Task ShowStepUpReauthAsync(TaskCompletionSource<string?> tcs)
    {
        if (_isShowingStepUp)
        {
            // Another step-up modal is already in flight. Complete this TCS
            // with null so the handler's await unblocks and surfaces the 403;
            // the in-flight modal will succeed for the user-visible request.
            tcs.TrySetResult(null);
            return;
        }
        _isShowingStepUp = true;

        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var stepUpPage = services?.GetService<StepUpReauthPage>();
            if (stepUpPage == null || Current?.MainPage == null)
            {
                _isShowingStepUp = false;
                tcs.TrySetResult(null);
                return;
            }

            stepUpPage.Tcs = tcs;

            var navPage = new NavigationPage(stepUpPage);
            navPage.Popped += (_, _) =>
            {
                if (navPage.Navigation.NavigationStack.Count <= 1)
                {
                    _isShowingStepUp = false;
                    // Safety net: if the page popped without completing the
                    // TCS (e.g. user swiped down on iOS), unblock the handler.
                    tcs.TrySetResult(null);
                }
            };
            await Current.MainPage.Navigation.PushModalAsync(navPage);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] ShowStepUpReauth error: {ex.Message}");
            _isShowingStepUp = false;
            tcs.TrySetResult(null);
        }
    }

    private async Task ShowAcceptTermsAsync()
    {
        if (_isShowingAcceptTerms) return;
        _isShowingAcceptTerms = true;

        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var termsPage = services?.GetService<AcceptTermsPage>();
            if (termsPage != null && Current?.MainPage != null)
            {
                var navPage = new NavigationPage(termsPage);
                navPage.Popped += (_, _) =>
                {
                    if (navPage.Navigation.NavigationStack.Count <= 1)
                        _isShowingAcceptTerms = false;
                };
                await Current.MainPage.Navigation.PushModalAsync(navPage);
            }
            else
            {
                _isShowingAcceptTerms = false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] ShowAcceptTerms error: {ex.Message}");
            _isShowingAcceptTerms = false;
        }
    }


    /// <summary>
    /// Points Syncfusion's theme at the current system theme.
    /// <para>
    /// The dictionary is found by type rather than by key because it is merged as a typed
    /// entry, and reassigning <c>VisualTheme</c> is what makes its controls repaint — replacing
    /// the whole dictionary would drop any colour overrides merged after it.
    /// </para>
    /// </summary>
    private void ApplySyncfusionTheme()
    {
        var theme = Resources?.MergedDictionaries
            .OfType<Syncfusion.Maui.Themes.SyncfusionThemeResourceDictionary>()
            .FirstOrDefault();

        if (theme is null) return;

        theme.VisualTheme = RequestedTheme == AppTheme.Dark
            ? Syncfusion.Maui.Themes.SfVisuals.MaterialDark
            : Syncfusion.Maui.Themes.SfVisuals.MaterialLight;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Determine the initial state based on onboarding/authentication status
        var state = _onboardingService.GetCurrentState(_tokenStorage, _apiSettings);

        Page startPage = state switch
        {
            OnboardingState.Welcome => CreateOnboardingNavigationPage(),
            OnboardingState.EmailVerification => CreateEmailVerificationPage(),
            OnboardingState.MustChangePassword => CreateForceChangePasswordPage(),
            OnboardingState.MustAcceptTerms => CreateAcceptTermsPage(),
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
            // Note: PendingSharedContact is handled by DashboardPage.OnAppearing
            // which fires after login is complete and Shell is ready
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

    private NavigationPage CreateForceChangePasswordPage()
    {
        var services = Handler?.MauiContext?.Services;
        if (services == null)
        {
            return CreateOnboardingNavigationPage();
        }

        var forcePage = services.GetRequiredService<ForceChangePasswordPage>();
        return new NavigationPage(forcePage);
    }

    private NavigationPage CreateAcceptTermsPage()
    {
        var services = Handler?.MauiContext?.Services;
        if (services == null)
        {
            return CreateOnboardingNavigationPage();
        }

        var termsPage = services.GetRequiredService<AcceptTermsPage>();
        return new NavigationPage(termsPage);
    }

    /// <summary>
    /// Builds the verification page itself. Separate from <see cref="CreateEmailVerificationPage"/>
    /// because a deep link arriving mid-onboarding pushes it onto the existing stack rather
    /// than replacing the root, which would discard the pages behind it.
    /// </summary>
    private EmailVerificationPage? BuildEmailVerificationPage()
    {
        var services = Handler?.MauiContext?.Services;
        if (services == null)
        {
            return null;
        }

        var email = _onboardingService.GetPendingVerificationEmail() ?? "";
        var householdName = ""; // TODO: Store household name in preferences if needed

        var apiClient = services.GetRequiredService<ShoppingApiClient>();
        return new EmailVerificationPage(apiClient, _onboardingService, email, householdName);
    }

    private NavigationPage CreateEmailVerificationPage()
    {
        var verificationPage = BuildEmailVerificationPage();
        if (verificationPage == null)
        {
            return CreateOnboardingNavigationPage();
        }

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

        // Refresh offline reminders on resume (self-hosted mode; the orchestrator self-gates)
        _ = SyncOfflineRemindersInBackgroundAsync();

        // Sync the app-icon badge to the current unread-notification count
        _ = AppBadgeHelper.RefreshAsync();

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
            // Note: PendingSharedContact is handled by DashboardPage.OnAppearing
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
    /// Refresh offline reminders in the background if enough time has elapsed. Self-hosted only —
    /// the orchestrator no-ops in cloud mode. This foreground trigger is the primary refresh path on
    /// iOS, where background tasks are unreliable (and don't run at all after a force-quit).
    /// </summary>
    private static async Task SyncOfflineRemindersInBackgroundAsync()
    {
        try
        {
            if (!NotificationSyncOrchestrator.ShouldSync(TimeSpan.FromHours(1)))
                return;

            var orchestrator = Current?.Handler?.MauiContext?.Services.GetService<NotificationSyncOrchestrator>();
            if (orchestrator == null) return;

            await orchestrator.SyncAsync();
        }
        catch (Exception ex)
        {
            // Swallow — background reminder sync is non-critical
            Console.WriteLine($"[App] Background reminder sync failed: {ex.Message}");
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

    private static async Task ProcessPendingSharedContactAsync()
    {
        if (PendingSharedContact == null) return;

        // Don't navigate if user isn't logged in -- keep PendingSharedContact for after login
        if (Shell.Current == null)
            return;

        var onboarding = Shell.Current.Handler?.MauiContext?.Services.GetService<OnboardingService>();
        var tokenStorage = Shell.Current.Handler?.MauiContext?.Services.GetService<TokenStorage>();
        var apiSettings = Shell.Current.Handler?.MauiContext?.Services.GetService<ApiSettings>();
        if (onboarding == null || tokenStorage == null || apiSettings == null)
            return;

        var state = onboarding.GetCurrentState(tokenStorage, apiSettings);
        if (state != OnboardingState.LoggedIn && state != OnboardingState.HomeSetupWizard)
            return;

        // Don't clear PendingSharedContact here -- ImportContactPage reads and clears it
        try
        {
            await Shell.Current.GoToAsync(nameof(ImportContactPage));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to process shared contact: {ex.Message}");
            PendingSharedContact = null;
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

            // Resolve the visible root through Windows first. The root is installed by
            // CreateWindow, which never assigns Application.MainPage, so testing MainPage
            // alone can miss the live page entirely and silently drop the link.
            var rootPage = Windows.FirstOrDefault()?.Page ?? Current?.MainPage;

            if (rootPage is NavigationPage navPage)
            {
                // Already on the verification page — hand it the token directly.
                if (navPage.CurrentPage is EmailVerificationPage verificationPage)
                {
                    verificationPage.HandleVerificationToken(token);
                    return;
                }

                // Otherwise show it. This branch was described in a comment but never
                // written, so tapping the emailed link anywhere else in the flow opened
                // the app and then did nothing at all. The page auto-verifies from the
                // token stored above when it appears.
                var pushed = BuildEmailVerificationPage();
                if (pushed != null)
                {
                    await navPage.PushAsync(pushed);
                    return;
                }
            }

            // No navigation stack to push onto (AppShell, or no window yet): swap the root
            // for the verification page. It must stay wrapped in a NavigationPage because a
            // successful verification pushes CreatePasswordPage onto the stack.
            var window = Windows.FirstOrDefault();
            if (window != null)
            {
                window.Page = CreateEmailVerificationPage();
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
    /// <summary>
    /// Routes a tapped offline-reminder deep link. Reminder links are the server's notification
    /// deep links (e.g. "/stock", "/todos", "/calendar/events/{id}") which are relative Shell routes,
    /// not absolute URIs — so they can't go through <see cref="HandleDeepLink"/>. Absolute links (if
    /// any) still fall back to it. Best-effort: an unknown route is ignored rather than crashing.
    /// </summary>
    public static void NavigateToReminderDeepLink(string? deepLink)
    {
        if (string.IsNullOrEmpty(deepLink)) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                if (Uri.TryCreate(deepLink, UriKind.Absolute, out var absolute))
                {
                    HandleDeepLink(absolute);
                }
                else if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync(deepLink);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Reminder deep link navigation failed: {ex.Message}");
            }
        });
    }

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

        // Phase 3 chunk 3.C — Universal Link / App Link OAuth callback:
        // https://app.famick.com/mobile-callback/oauth/{provider}?code=...&state=...
        // This is the parallel HTTPS path to the existing custom-scheme
        // com.famick.homemanagement://oauth/callback. The OS verifies domain
        // ownership against AASA (iOS) / assetlinks.json (Android) before
        // handing us the link, so when this fires we know the URL came from
        // the OS, not an arbitrary app on the device. Routes into the same
        // resume code path as WebAuthenticator's in-process result. Server-
        // side cloud route also 302-redirects this to the custom scheme for
        // browser-only (app-not-installed) callers, so a deep-link hit here
        // means the OS already picked Famick to handle it.
        if (uri.AbsolutePath.StartsWith("/mobile-callback/oauth/", StringComparison.OrdinalIgnoreCase))
        {
            var segments = uri.AbsolutePath.Trim('/').Split('/');
            if (segments.Length >= 3)
            {
                var provider = segments[2];
                var code = query.GetValueOrDefault("code");
                var state = query.GetValueOrDefault("state");
                var error = query.GetValueOrDefault("error");

                if (Current?.MainPage != null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        try
                        {
                            var services = (Current as App)?.Handler?.MauiContext?.Services;
                            var oauthService = services?.GetService<OAuthService>();
                            if (oauthService is not null)
                            {
                                var oauthResult = await oauthService.ResumeFromUniversalLinkAsync(provider, code, state, error);
                                // Phase 4 follow-up — UL/AL resume is the
                                // only OAuth path without a Page-level
                                // handler that can push the prompt itself.
                                if (oauthResult?.LocalServerChange is not null)
                                {
                                    await ShowLocalServerChangePromptAsync(oauthResult.LocalServerChange);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[App] OAuth UL/AL dispatch failed: {ex.Message}");
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
    /// Transitions back to the onboarding (Welcome) page. Use after
    /// a full reset of the app — clears the main shell and drops the
    /// user at the initial welcome screen so they can re-onboard.
    /// </summary>
    public static void TransitionToOnboarding()
    {
        Console.WriteLine("[App.TransitionToOnboarding] Called");
        if (Current is not App app) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                Current.MainPage = app.CreateOnboardingNavigationPage();
                Console.WriteLine("[App.TransitionToOnboarding] MainPage set to onboarding");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App.TransitionToOnboarding] Error: {ex.Message}");
            }
        });
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

                // Tell the user if signing in just called off their scheduled deletion.
                await ShowDeletionCancelledNoticeAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App.TransitionToMainApp] Error: {ex.Message}");
                Console.WriteLine($"[App.TransitionToMainApp] Stack: {ex.StackTrace}");
            }
        });
    }

    /// <summary>
    /// Tells the user when signing in has just called off a scheduled deletion.
    /// </summary>
    /// <remarks>
    /// Signing in cancels a deletion, so it can be cancelled by the ordinary act of
    /// opening the app rather than by anyone deciding to. Without this, someone who meant
    /// it to go ahead finds out only by noticing their data is still there — and the
    /// thirty-day clock they were counting on has quietly restarted at zero.
    /// <para>
    /// The notice is acknowledged only after the alert has been dismissed, so a failure
    /// anywhere earlier leaves it to be shown next time rather than swallowing it.
    /// </para>
    /// </remarks>
    private static async Task ShowDeletionCancelledNoticeAsync()
    {
        try
        {
            var services = Current?.Handler?.MauiContext?.Services;
            var apiClient = services?.GetService<ShoppingApiClient>();
            if (apiClient == null) return;

            var status = await apiClient.GetAccountDeletionStatusAsync();
            if (!status.Success || status.Data?.CancelledNotice is not { } notice) return;

            var page = Current?.MainPage;
            if (page == null) return;

            var requestedOn = notice.RequestedAt.ToLocalTime().ToString("D");
            var subject = notice.WasHousehold ? "This household was" : "Your account was";

            await page.DisplayAlert(
                "Deletion cancelled",
                $"{subject} scheduled for deletion on {requestedOn}. Signing in has cancelled it, "
                + "and nothing has been deleted.\n\n"
                + "If you still want to go ahead, request deletion again from Security in your profile.",
                "OK");

            await apiClient.AcknowledgeDeletionNoticeAsync();
        }
        catch (Exception ex)
        {
            // Never block entry to the app over a notice; it will be shown next sign-in.
            Console.WriteLine($"[App.ShowDeletionCancelledNotice] {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the app to the sign-in screen, replacing the whole navigation stack.
    /// </summary>
    /// <remarks>
    /// The counterpart to <see cref="TransitionToMainApp"/>, which is unconditional — it
    /// sets <c>MainPage</c> to the shell without consulting whether anyone is signed in.
    /// Calling it after clearing tokens lands the user on the home page with no session,
    /// which is how account deletion first behaved: a flash of the login screen, then the
    /// app again.
    /// <para>
    /// The stack is replaced rather than pushed over so nothing remains to navigate back
    /// into — after signing out of a household that is being deleted, a live back stack
    /// would just produce 401s against data on its way out.
    /// </para>
    /// </remarks>
    public static void TransitionToLogin()
    {
        if (Current == null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                var services = Current.Handler?.MauiContext?.Services;
                var loginPage = services?.GetService<LoginPage>();

                if (loginPage == null)
                {
                    Console.WriteLine("[App.TransitionToLogin] LoginPage could not be resolved");
                    return;
                }

                Current.MainPage = new NavigationPage(loginPage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App.TransitionToLogin] Error: {ex.Message}");
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
