using Famick.HomeManagement.Core;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.UI.Localization;
using Famick.HomeManagement.UI.Services;
using Famick.HomeManagement.Web.Client;
using Famick.HomeManagement.Web.Client.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient with base address
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Add MudBlazor services
builder.Services.AddMudServices();

// Add localization services
builder.Services.AddScoped<ILanguagePreferenceStorage, BrowserLanguagePreferenceStorage>();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();
builder.Services.AddScoped<ILocalizer, Localizer>();
builder.Services.AddTransient<MudLocalizer, FamickMudLocalizer>();

// Add authentication services
builder.Services.AddScoped<ITokenStorage, BrowserTokenStorage>();
builder.Services.AddScoped<IApiClient, HttpApiClient>();
builder.Services.AddScoped<ApiAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<ApiAuthStateProvider>());

// Add user permissions service for role-based UI restrictions
builder.Services.AddScoped<IUserPermissions, UserPermissions>();

// Add mobile detection service for deep linking support
builder.Services.AddScoped<IMobileDetectionService, MobileDetectionService>();

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
