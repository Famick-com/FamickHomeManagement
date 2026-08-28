using Famick.HomeManagement.Core;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Shared.Net;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Services;
using Famick.HomeManagement.Web.Client;
using Famick.HomeManagement.Web.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;
using Syncfusion.Blazor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

if (!string.IsNullOrEmpty(LicenseKeys.Syncfusion))
{
    Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(LicenseKeys.Syncfusion);
}

// Configure HttpClient with base address
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Add MudBlazor services
builder.Services.AddMudServices();

builder.Services.AddSyncfusionBlazor();

// Add localization services
builder.Services.AddScoped<ILanguagePreferenceStorage, BrowserLanguagePreferenceStorage>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<ILocalizer, Localizer>();
builder.Services.AddTransient<MudLocalizer, FamickMudLocalizer>();

// Phase 3 chunk 3.B — open-redirect host allow-list. Required by Login.razor
// and ExternalAuthCallback.razor (which @inject IRedirectUrlValidator). Client-
// side validator uses an empty allow-list — the WASM client only ever
// NavigateTo's relative URLs (handled by RedirectUrlValidator without needing
// any allowed-hosts entries); absolute URLs would be rejected and replaced
// with the safe default by the components themselves.
builder.Services.Configure<RedirectUriAllowListOptions>(_ => { });
builder.Services.AddSingleton<IRedirectUrlValidator, RedirectUrlValidator>();

// Add authentication services
builder.Services.AddScoped<ITokenStorage, BrowserTokenStorage>();
// Light/dark choice. Scoped so the sign-in screen and the app behind it read the same value —
// they used to disagree, the app having a toggle while the pre-auth pages were pinned light.
builder.Services.AddScoped<Famick.HomeManagement.UI.Services.IThemePreference,
    Famick.HomeManagement.UI.Services.ThemePreference>();
// Phase 2.5 — coordinator shows the reauth modal on 403 STEP_UP_REQUIRED.
// Registered before IApiClient so HttpApiClient resolves it via DI.
builder.Services.AddScoped<IStepUpReauthCoordinator, StepUpReauthCoordinator>();
builder.Services.AddScoped<IApiClient, HttpApiClient>();
builder.Services.AddScoped<ApiAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthStateProvider>());

// Add user permissions service for role-based UI restrictions
builder.Services.AddScoped<IUserPermissions, UserPermissions>();

// Caches the server's ServerPlatform (from the anonymous boot status endpoint)
// so pages can adapt UI per platform without re-deriving config flags.
builder.Services.AddScoped<PlatformState>();

// Add mobile detection service for deep linking support
builder.Services.AddScoped<IMobileDetectionService, MobileDetectionService>();

// Add subscription state provider for client-side feature gating
builder.Services.AddScoped<SubscriptionStateProvider>();
builder.Services.AddScoped<ISubscriptionStateProvider>(sp => sp.GetRequiredService<SubscriptionStateProvider>());

// Configure authorization policies — must match AuthorizationPolicies.Configure in Web.Shared
builder.Services.AddAuthorizationCore(options =>
{
    options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("RequireEditor", policy => policy.RequireRole("Admin", "Editor"));
    options.AddPolicy("RequireViewer", policy => policy.RequireRole("Admin", "Editor", "Viewer"));
});

// Add barcode scanner service (web stub - camera not available in browser)
builder.Services.AddScoped<IBarcodeScannerService, WebBarcodeScannerService>();

// Add inventory session service
builder.Services.AddScoped<IInventorySessionService, BrowserInventorySessionService>();

// Add shopping list preference storage
builder.Services.AddScoped<IShoppingListPreferenceStorage, BrowserShoppingListPreferenceStorage>();

// Add navigation menu preference storage
builder.Services.AddScoped<INavMenuPreferenceStorage, BrowserNavMenuPreferenceStorage>();

builder.Services.AddCore(builder.Configuration);

await builder.Build().RunAsync();
