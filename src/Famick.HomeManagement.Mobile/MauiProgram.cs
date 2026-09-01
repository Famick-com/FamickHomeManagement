using CommunityToolkit.Maui;
using Famick.HomeManagement.Mobile.Pages;
using Famick.HomeManagement.Mobile.Pages.Calendar;
using Famick.HomeManagement.Mobile.Pages.Chores;
using Famick.HomeManagement.Mobile.Pages.Equipment;
using Famick.HomeManagement.Mobile.Pages.StorageBins;
using Famick.HomeManagement.Mobile.Pages.Settings;
using Famick.HomeManagement.Mobile.Pages.Stores;
using Famick.HomeManagement.Mobile.Pages.Household;
using Famick.HomeManagement.Mobile.Pages.Contacts;
using Famick.HomeManagement.Mobile.Pages.Onboarding;
using Famick.HomeManagement.Mobile.Pages.Products.ProductOnboarding;
using Famick.HomeManagement.Mobile.Pages.Profile;
using Famick.HomeManagement.Mobile.Pages.Recipes;
using Famick.HomeManagement.Mobile.Pages.Wizard;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Messaging;
using Famick.HomeManagement.Mobile.Services;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;
using ZXing.Net.Maui.Controls;

namespace Famick.HomeManagement.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        if (!string.IsNullOrEmpty(LicenseKeys.Syncfusion))
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(LicenseKeys.Syncfusion);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureSyncfusionCore()
            .UseBarcodeReader()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MauiMaterialAssets.ttf", "MaterialAssets");
            });

#if IOS
        // Disable iOS Password AutoFill floating button
        Platforms.iOS.DisableAutoFillHandler.Register();
#endif

#if ANDROID
        // Disable icon tinting for toolbar so the notification bell red dot is visible
        Microsoft.Maui.Handlers.ToolbarHandler.Mapper.AppendToMapping(
            "PreserveNotificationBellColors", (handler, view) =>
            {
                if (handler.PlatformView is AndroidX.AppCompat.Widget.Toolbar toolbar)
                {
                    toolbar.Post(() =>
                    {
                        var menu = toolbar.Menu;
                        if (menu == null) return;
                        for (int i = 0; i < menu.Size(); i++)
                        {
                            var menuItem = menu.GetItem(i);
                            menuItem?.Icon?.SetTintList(null);
                        }
                    });
                }
            });
#endif

        // API Settings (singleton - configures server URL)
        var apiSettings = new ApiSettings();
        builder.Services.AddSingleton(apiSettings);

        // Configure HttpClient with dynamic base URL and automatic token refresh.
        //
        // BaseAddress is a STABLE, REAL ORIGIN — not the actually-used URL.
        // HttpClient snapshots BaseAddress at construction time and uses
        // it to resolve relative URIs into absolute ones. If we set the
        // proxied base (e.g. ".../h/{guid}/") here, every relative URI
        // like "api/auth/login" would end up as ".../h/{guid}/api/auth/login"
        // in PathAndQuery, which DynamicApiHttpHandler would then prepend
        // ".../h/{guid}/" AGAIN to. The actual outbound URL is determined
        // by DynamicApiHttpHandler reading ApiSettings.BaseUrl per-request.
        //
        // The chosen origin (AuthProxy public base, no path) only matters
        // before any request is rewritten — if some lower-level code paths
        // resolve the host eagerly, this one is real and reachable so the
        // app doesn't fail before the handler chain kicks in.
        var apiHttpClientBaseAddress = new Uri(ApiSettings.AuthProxyPublicBaseUrl + "/");
        builder.Services.AddScoped(sp =>
        {
            var settings = sp.GetRequiredService<ApiSettings>();
            var tokenStorage = sp.GetRequiredService<TokenStorage>();
            var innerHandler = new DynamicApiHttpHandler(settings);
            var authHandler = new AuthenticatingHttpHandler(tokenStorage, settings)
            {
                InnerHandler = innerHandler
            };
            return new HttpClient(authHandler)
            {
                BaseAddress = apiHttpClientBaseAddress,
                Timeout = TimeSpan.FromSeconds(30)
            };
        });

        // Proxied-mode plumbing — email-lookup against AuthProxy and the
        // local cache that lets repeat sign-ins skip the round-trip.
        // Both are stateless / Preferences-backed → singleton.
        builder.Services.AddSingleton<EmailLookupApi>();
        builder.Services.AddSingleton<ProxiedEmailCache>();

        // Core Services
        builder.Services.AddSingleton<TokenStorage>();
        builder.Services.AddSingleton<SyncAccountScope>();
        builder.Services.AddSingleton<TenantStorage>();
        // Phase 4 chunk 4.H — short-timeout LAN reachability probe with 60s
        // negative cache. Singleton so the cache survives across requests.
        builder.Services.AddSingleton<LocalServerProbeService>();
        builder.Services.AddSingleton<SubscriptionStateService>();
        builder.Services.AddSingleton<ISubscriptionStateProvider>(sp => sp.GetRequiredService<SubscriptionStateService>());
        builder.Services.AddSingleton<OnboardingService>();
        builder.Services.AddScoped<ShoppingApiClient>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<OfflineStorageService>();
        builder.Services.AddSingleton<BleScannerService>();
        builder.Services.AddSingleton<IMessageBus, MessageBus>();
        builder.Services.AddSingleton<MessageBusAdapter>();
        builder.Services.AddScoped<ImageCacheService>();

        // ConnectivityService needs ShoppingApiClient, register as scoped to match ShoppingApiClient's lifetime
        builder.Services.AddScoped<ConnectivityService>();

        // OAuth Service for social login
        builder.Services.AddScoped<OAuthService>();

        // Store integration OAuth service
        builder.Services.AddScoped<StoreIntegrationOAuthService>();

        // Platform-specific Apple Sign in service
#if IOS
        builder.Services.AddSingleton<IAppleSignInService, Platforms.iOS.AppleSignInService>();
#elif ANDROID
        builder.Services.AddSingleton<IAppleSignInService, Platforms.Android.AppleSignInService>();
#endif

        // Phase 2.5b — platform-specific passkey (WebAuthn) authenticator.
        // iOS impl uses ASAuthorizationPlatformPublicKeyCredentialProvider (iOS 16+).
        // Android impl uses androidx.credentials.CredentialManager (API 28+).
        // Both are runtime-gated via IPasskeyAuthenticator.IsSupported so
        // older devices keep working — the page's button visibility binds to
        // this property.
#if IOS
        builder.Services.AddSingleton<IPasskeyAuthenticator, Platforms.iOS.PasskeyAuthenticator>();
#elif ANDROID
        builder.Services.AddSingleton<IPasskeyAuthenticator, Platforms.Android.PasskeyAuthenticator>();
#endif

        // Platform-specific Google Sign in service
#if IOS
        builder.Services.AddSingleton<IGoogleSignInService, Platforms.iOS.GoogleSignInService>();
#elif ANDROID
        builder.Services.AddSingleton<IGoogleSignInService, Platforms.Android.GoogleSignInService>();
#endif

        // Platform-specific push notification token provider
#if IOS
        builder.Services.AddSingleton<IPushTokenProvider, Platforms.iOS.PushTokenProvider>();
#elif ANDROID
        builder.Services.AddSingleton<IPushTokenProvider, Platforms.Android.PushTokenProvider>();
#endif
        builder.Services.AddScoped<PushNotificationRegistrationService>();

        // Platform-specific contact sync service
#if IOS
        builder.Services.AddSingleton<IContactSyncService, Platforms.iOS.ContactSyncService>();
        builder.Services.AddSingleton<IDeviceContactPicker, Platforms.iOS.DeviceContactPicker>();
#elif ANDROID
        builder.Services.AddSingleton<IContactSyncService, Platforms.Android.ContactSyncService>();
        builder.Services.AddSingleton<IDeviceContactPicker, Platforms.Android.DeviceContactPicker>();
#endif
        builder.Services.AddSingleton<ContactSyncMappingStore>();
        builder.Services.AddScoped<ContactSyncOrchestrator>();

        // Platform-specific calendar sync service
#if IOS
        builder.Services.AddSingleton<ICalendarSyncService, Platforms.iOS.CalendarSyncService>();
#elif ANDROID
        builder.Services.AddSingleton<ICalendarSyncService, Platforms.Android.CalendarSyncService>();
#endif
        builder.Services.AddSingleton<CalendarSyncMappingStore>();
        builder.Services.AddScoped<CalendarSyncOrchestrator>();

        // Onboarding Pages (only those that can be resolved by DI)
        // Note: EmailVerificationPage and CreatePasswordPage have runtime parameters
        // and are created manually during navigation
        builder.Services.AddTransient<WelcomePage>();
        builder.Services.AddTransient<QrScannerPage>();

        // Main App Pages (registered for DI navigation)
        builder.Services.AddTransient<DashboardPage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<ForceChangePasswordPage>();
        builder.Services.AddTransient<AcceptTermsPage>();
        builder.Services.AddTransient<StepUpReauthPage>();
        builder.Services.AddTransient<ServerConfigPage>();
        builder.Services.AddTransient<ListSelectionPage>();
        builder.Services.AddTransient<ShoppingSessionPage>();
        builder.Services.AddTransient<AddItemPage>();
        builder.Services.AddTransient<BarcodeScannerPage>();
        builder.Services.AddTransient<AisleOrderPage>();
        builder.Services.AddTransient<SettingsPage>();
        builder.Services.AddTransient<AboutPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<NotificationSettingsPage>();
        builder.Services.AddTransient<BarcodeScannerSettingsPage>();
        builder.Services.AddTransient<QuickConsumePage>();
        builder.Services.AddTransient<ChildProductSelectionPage>();
        builder.Services.AddTransient<InventorySessionPage>();
        builder.Services.AddTransient<StockOverviewPage>();
        builder.Services.AddTransient<ProductsListPage>();
        builder.Services.AddTransient<ProductDetailPage>();
        builder.Services.AddTransient<ProductEditPage>();

        // Calendar Pages
        builder.Services.AddTransient<CalendarPage>();
        builder.Services.AddTransient<CalendarEventDetailPage>();
        builder.Services.AddTransient<CreateEditEventPage>();

        // Household Pages
        builder.Services.AddTransient<HouseholdOverviewPage>();
        builder.Services.AddTransient<HouseholdOverviewEditPage>();
        builder.Services.AddTransient<HouseholdUtilitiesPage>();
        builder.Services.AddTransient<HouseholdHomeCarePage>();
        builder.Services.AddTransient<HouseholdHomeCareEditPage>();
        builder.Services.AddTransient<HouseholdFinancialPage>();
        builder.Services.AddTransient<HouseholdFinancialEditPage>();

        // Equipment Pages
        builder.Services.AddTransient<EquipmentListPage>();
        builder.Services.AddTransient<EquipmentDetailPage>();
        builder.Services.AddTransient<EquipmentEditPage>();

        // Storage Bin Pages
        builder.Services.AddTransient<StorageBinListPage>();
        builder.Services.AddTransient<StorageBinDetailPage>();
        builder.Services.AddTransient<StorageBinEditPage>();

        // Settings Pages
        builder.Services.AddTransient<StorageLocationsPage>();

        // Store Pages
        builder.Services.AddTransient<StoresListPage>();
        builder.Services.AddTransient<StoreDetailPage>();
        builder.Services.AddTransient<StoreEditPage>();
        builder.Services.AddTransient<StoreIntegrationLinkPage>();

        // Task Pages
        builder.Services.AddTransient<Pages.Tasks.TasksListPage>();
        builder.Services.AddTransient<Pages.Tasks.TaskEditPage>();
        builder.Services.AddTransient<Pages.Tasks.TaskWizardPage>();

        // Chore Pages
        builder.Services.AddTransient<ChoresListPage>();
        builder.Services.AddTransient<ChoreDetailPage>();
        builder.Services.AddTransient<ChoreEditPage>();

        // Recipe Pages
        builder.Services.AddTransient<RecipeListPage>();
        builder.Services.AddTransient<RecipeDetailPage>();
        builder.Services.AddTransient<RecipeEditPage>();
        builder.Services.AddTransient<RecipeStepsPage>();
        builder.Services.AddTransient<AddIngredientPage>();

        // Meal Planner Pages
        builder.Services.AddTransient<Pages.MealPlanner.MealPlannerSettingsPage>();
        builder.Services.AddTransient<Pages.MealPlanner.MealPlannerPage>();
        builder.Services.AddTransient<Pages.MealPlanner.MealsListPage>();
        builder.Services.AddTransient<Pages.MealPlanner.MealDetailPage>();
        builder.Services.AddTransient<Pages.MealPlanner.MealSelectionPage>();
        builder.Services.AddTransient<Pages.MealPlanner.MealEditPage>();

        // Contact Pages
        builder.Services.AddTransient<ContactGroupsPage>();
        builder.Services.AddTransient<ContactGroupDetailPage>();
        builder.Services.AddTransient<ContactGroupEditPage>();
        builder.Services.AddTransient<ContactDetailPage>();
        builder.Services.AddTransient<ContactEditPage>();
        builder.Services.AddTransient<ContactAuditLogPage>();
        builder.Services.AddTransient<ContactTagsPage>();
        builder.Services.AddTransient<MemberAccountManagePage>();
        builder.Services.AddTransient<ImportContactPage>();
        builder.Services.AddTransient<SelectHouseholdPage>();

        // Profile Pages
        builder.Services.AddTransient<ProfilePersonalInfoPage>();
        builder.Services.AddTransient<ProfileCalendarPage>();
        builder.Services.AddTransient<ProfileSecurityPage>();
        builder.Services.AddTransient<DeleteAccountPage>();
        builder.Services.AddTransient<ProfileContactSyncPage>();

        // Product Onboarding Pages
        builder.Services.AddTransient<ProductOnboardingIntroPage>();
        builder.Services.AddTransient<ProductOnboardingHouseholdPage>();
        builder.Services.AddTransient<ProductOnboardingDietaryPage>();
        builder.Services.AddTransient<ProductOnboardingGetStartedPage>();

        // Wizard Pages
        builder.Services.AddTransient<WizardHouseholdInfoPage>();
        builder.Services.AddTransient<WizardMembersPage>();
        builder.Services.AddTransient<WizardHomeStatsPage>();
        builder.Services.AddTransient<WizardMaintenancePage>();
        builder.Services.AddTransient<WizardVehiclesPage>();
        // Note: WizardVehicleEditPage has runtime parameters and is created manually

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
