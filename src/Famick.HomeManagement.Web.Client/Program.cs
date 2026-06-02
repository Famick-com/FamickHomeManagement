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

// Phase 5 chunk 5.K — auth-host routing for the cloud SPA. The handler reads
// the persisted use_auth_famick_com flag (BrowserAuthHostFlagStorage,
// localStorage-backed) and rewrites api/auth/* and /check requests to
// auth.famick.com when on. AddHttpClient gives us the WASM runtime's
// fetch-based primary handler underneath the DelegatingHandler.
builder.Services.AddScoped<IAuthHostFlagStorage, BrowserAuthHostFlagStorage>();
builder.Services.AddScoped<AuthHostRoutingHandler>();

builder.Services.AddHttpClient("Famick.Default", client =>
{
    client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress);
}).AddHttpMessageHandler<AuthHostRoutingHandler>();

builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("Famick.Default"));

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
// Phase 2.5 — coordinator shows the reauth modal on 403 STEP_UP_REQUIRED.
// Registered before IApiClient so HttpApiClient resolves it via DI.
builder.Services.AddScoped<IStepUpReauthCoordinator, StepUpReauthCoordinator>();
builder.Services.AddScoped<IApiClient, HttpApiClient>();
builder.Services.AddScoped<ApiAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthStateProvider>());

// Add user permissions service for role-based UI restrictions
builder.Services.AddScoped<IUserPermissions, UserPermissions>();

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
