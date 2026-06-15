using AspNetCoreRateLimit;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Middleware;
using Famick.HomeManagement.Web.Services;
using Famick.HomeManagement.Web.Cli;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Infrastructure;
using Famick.HomeManagement.Jobs;
using Famick.HomeManagement.Logging.Redaction;
using Famick.HomeManagement.Shared.Captcha;
using Famick.HomeManagement.Web.Shared;
using Famick.HomeManagement.Web.Shared.Authentication;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Core;
using Famick.HomeManagement.Core.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Microsoft.EntityFrameworkCore;

// Handle CLI commands before starting web host
if (args.Length >= 1 && args[0] == "admin-cli")
{
    return await AdminCli.RunAsync(args[1..]);
}

// Job-runner mode: same DI graph as web mode, but exits after running one job.
// Invoked as: dotnet Famick.HomeManagement.Web.dll run-job <job-name>
var isJobMode = args.Length >= 2 && args[0] == "run-job";
var jobKey = isJobMode ? args[1] : null;
var builderArgs = isJobMode ? args[2..] : args;

var builder = WebApplication.CreateBuilder(builderArgs);

// Add optional configuration from mounted volume (for Docker deployments)
// This allows users to override settings without rebuilding the image.
// Legacy name kept loading for one release so existing self-hosters aren't broken.
var legacyConfigPath = Path.Combine(builder.Environment.ContentRootPath, "config", "appsettings.json");
if (File.Exists(legacyConfigPath))
{
    builder.Configuration.AddJsonFile(legacyConfigPath, optional: true, reloadOnChange: true);
}

// Canonical self-hosted overlay. Wins over the legacy file when both exist
// (later AddJsonFile calls take precedence in IConfiguration). The wizard and
// admin "Server Settings" page write this file; operators can also edit it
// directly on the host.
//
// Storage:Path is the single source of truth for "where mutable data lives" —
// plugins, server-config overlay, Data Protection keys, and uploads all derive
// from it (each individually overridable). Relative paths resolve against
// ContentRootPath so the setting means the same thing regardless of launch dir.
var storageRoot = StoragePaths.ResolveStorageRoot(builder.Configuration, builder.Environment.ContentRootPath);
var serverConfigPath = StoragePaths.ResolveServerConfigPath(
    builder.Configuration, builder.Environment.ContentRootPath, storageRoot);
builder.Configuration.AddJsonFile(serverConfigPath, optional: true, reloadOnChange: true);

// Configure Serilog. The bootstrap logger handles the handful of log calls that
// happen before builder.Build() — it intentionally skips redaction because DI
// isn't available yet and the bootstrap logs (reverse-proxy config, etc.) don't
// carry secrets. The runtime logger (configured via UseSerilog below) wires the
// Phase 3 redaction enricher via DI so every request/response log gets scrubbed.
// Console only — docker captures stdout into `docker compose logs`, so the
// previous WriteTo.File sink was duplicating to ./logs with no consumer.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog((ctx, services, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithFamickRedaction(services)
    .WriteTo.Console());

// Add services to the container
builder.Services.AddControllersWithViews();

// Add API controllers with JSON options
builder.Services.AddControllers(options =>
    {
        // Phase 2 — runs on every action; no-op unless [StepUp] is present.
        options.Filters.Add<Famick.HomeManagement.Web.Shared.Authorization.StepUpFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Configure IP rate limiting
builder.Services.AddMemoryCache();
builder.Services.AddDistributedMemoryCache(); // IDistributedCache for product search caching (swap to Redis for cloud)
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.Configure<IpRateLimitPolicies>(builder.Configuration.GetSection("IpRateLimitPolicies"));
builder.Services.AddInMemoryRateLimiting();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();

// Configure forwarded headers for reverse proxy (nginx, etc.)
var trustedProxies = builder.Configuration.GetSection("ReverseProxy:TrustedProxies").Get<string[]>();
var trustedNetworks = builder.Configuration.GetSection("ReverseProxy:TrustedNetworks").Get<string[]>();

Log.Information("Reverse Proxy Configuration - TrustedProxies: {Proxies}, TrustedNetworks: {Networks}",
    trustedProxies != null ? string.Join(", ", trustedProxies) : "(none - trust all)",
    trustedNetworks != null ? string.Join(", ", trustedNetworks) : "(none)");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;

    if (trustedProxies?.Length > 0 || trustedNetworks?.Length > 0)
    {
        // Use explicitly configured proxies/networks
        if (trustedProxies != null)
        {
            foreach (var proxy in trustedProxies)
            {
                if (IPAddress.TryParse(proxy, out var ip))
                    options.KnownProxies.Add(ip);
            }
        }

        if (trustedNetworks != null)
        {
            foreach (var network in trustedNetworks)
            {
                var parts = network.Split('/');
                if (parts.Length == 2 &&
                    IPAddress.TryParse(parts[0], out var ip) &&
                    int.TryParse(parts[1], out var prefix))
                {
#pragma warning disable ASPDEPR005 // KnownNetworks is obsolete
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(ip, prefix));
#pragma warning restore ASPDEPR005
                }
            }
        }
    }
    else
    {
        // Default: trust all proxies (for simple Docker setups)
#pragma warning disable ASPDEPR005 // KnownNetworks is obsolete
        options.KnownNetworks.Clear();
#pragma warning restore ASPDEPR005
        options.KnownProxies.Clear();
    }
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddFeatureFlags(builder.Configuration);
builder.Services.AddLoggingRedaction().AddDefaultRedactors();
// Phase 4 chunk 4.B — captcha gate. Self-hosted leaves RecaptchaSettings.SecretKey
// empty, which binds NoOpCaptchaService (no public-internet abuse surface).
builder.Services.AddCaptcha(builder.Configuration);

// Phase 3 chunk 3.B — open-redirect host allow-list. The validator gates every
// user-supplied `returnUrl` / `ReturnUrl` sink in the codebase so an attacker
// can't trick the login flow into bouncing back through an attacker-controlled
// host. Allow-list is bound from RedirectUriAllowList:Hosts in appsettings.
builder.Services.Configure<Famick.HomeManagement.Shared.Net.RedirectUriAllowListOptions>(
    builder.Configuration.GetSection(
        Famick.HomeManagement.Shared.Net.RedirectUriAllowListOptions.SectionName));
builder.Services.AddSingleton<
    Famick.HomeManagement.Shared.Net.IRedirectUrlValidator,
    Famick.HomeManagement.Shared.Net.RedirectUrlValidator>();
builder.Services.AddScoped<Famick.HomeManagement.Web.Shared.Authorization.StepUpFilter>();

// Create the JWT signing key service ONCE and register the same instance as the DI singleton.
// This avoids the BuildServiceProvider anti-pattern which created two separate RSA keys:
// one for the middleware and a different one for TokenService, causing all tokens to fail validation.
var signingKeyService = new JwtSigningKeyService(
    builder.Configuration,
    LoggerFactory.Create(b => b.AddSerilog()).CreateLogger<JwtSigningKeyService>(),
    builder.Environment);
builder.Services.AddSingleton<IJwtSigningKeyService>(signingKeyService);

builder.Services.AddCore(builder.Configuration);

// Configure app store links for mobile deep linking
builder.Services.Configure<AppStoreLinksSettings>(
    builder.Configuration.GetSection("AppStoreLinks"));

// Server-level overlay editable via wizard / admin UI / host filesystem.
// Inject as IOptionsMonitor<ServerOptions> to pick up file changes without restart.
builder.Services.Configure<ServerOptions>(
    builder.Configuration.GetSection(ServerOptions.SectionName));

// Register HttpContextAccessor for tenant resolution from HTTP context
builder.Services.AddHttpContextAccessor();

// Phase 4 chunk 4.D — register IMultiTenancyOptions explicitly.
// Self-hosted is single-tenant. Until now every consumer accepted
// IMultiTenancyOptions as a nullable optional with a multi-tenant=true
// fallback — that was a latent bug since the fallback gave the wrong answer
// on self-hosted, masked by the fact that the existing consumers also
// fell back to FixedTenantId / TenantProvider for the actual lookup.
// LocalServerResolver uses IsMultiTenantEnabled as a real gate so this has
// to be correct.
builder.Services.AddSingleton<Famick.HomeManagement.Infrastructure.Configuration.IMultiTenancyOptions>(
    new Famick.HomeManagement.Infrastructure.Configuration.MultiTenancyOptions
    {
        IsMultiTenantEnabled = false,
        FixedTenantId = null,
    });

// Register TenantProvider (Fixed Tenant for self-hosted)
var fixedTenantId = builder.Configuration.GetValue<Guid>("FixedTenantId");
if (fixedTenantId == Guid.Empty)
{
    fixedTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}

// Tenant-config consistency guard. FixedTenantId scopes per-request
// queries (via FixedTenantProvider); SelfHosted:TenantId tags new rows
// written by auth services (AuthenticationService / ExternalAuthService
// / PasskeyService — users, refresh tokens, passkeys, etc.). When the
// two diverge, new rows accumulate under one tenant while queries read
// from another — no error surfaces until the operator notices empty UI
// views weeks later. Refuse to start in Production when the effective
// UUIDs differ. Dev keeps the auto-fallback so unconfigured installs
// still boot.
if (builder.Environment.IsProduction())
{
    var selfHostedTenantId = Guid.TryParse(builder.Configuration["SelfHosted:TenantId"], out var parsed)
        ? parsed
        : Guid.Parse("00000000-0000-0000-0000-000000000001");
    if (fixedTenantId != selfHostedTenantId)
    {
        throw new InvalidOperationException(
            $"Tenant configuration mismatch: FixedTenantId resolves to {fixedTenantId} " +
            $"but SelfHosted:TenantId resolves to {selfHostedTenantId}. These two config " +
            "keys feed different code paths (per-request query scope vs auth-service " +
            "writes); mismatched values silently split data across two tenants. Set both " +
            "env vars (FixedTenantId and SelfHosted__TenantId) to the same UUID and redeploy.");
    }
}

builder.Services.AddScoped<ITenantProvider>(sp =>
    new FixedTenantProvider(fixedTenantId, sp.GetRequiredService<IHttpContextAccessor>(), sp.GetRequiredService<ILogger<FixedTenantProvider>>()));

// Configure Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// Bind HA Ingress settings + register the user resolver unconditionally.
// The handler short-circuits when HaIngress:Enabled is false, so adding
// this is a no-op for non-add-on deployments.
builder.Services.AddHaIngressUserResolution(builder.Configuration);

builder.Services.AddAuthentication(options =>
{
    // Multiplex scheme forwards to HaIngress when the X-Remote-User-Id header
    // is present, otherwise to JwtBearer — so the default code path is
    // unchanged when running outside an HA add-on.
    options.DefaultAuthenticateScheme = HaIngressAuthenticationDefaults.MultiplexPolicyScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Prevent automatic claim type mapping (keeps "sub" as-is instead of mapping to long URI)
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        // Phase 1 — accept any active signing key (current + previous-during-overlap).
        IssuerSigningKeys = signingKeyService.ActiveValidationKeys,
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub",  // Use "sub" claim as the user identifier
        RoleClaimType = "role"  // Match short claim name when MapInboundClaims is false
    };
})
.AddHaIngress()
.AddHaIngressOrFallbackPolicyScheme(JwtBearerDefaults.AuthenticationScheme);

// Configure authorization policies for role-based access
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

// Register file storage service (for product images and equipment documents)
// Files are stored under {Storage:Path}/uploads (default data/uploads) — same
// physical mount as plugin config, server config, and Data Protection keys.
// Served through authenticated API endpoints, not static files.
var uploadsPath = StoragePaths.ResolveUploadsPath(
    builder.Configuration, builder.Environment.ContentRootPath, storageRoot);
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<LocalFileStorageService>>();
    var baseUrl = builder.Configuration["BaseUrl"] ?? "";
    return new LocalFileStorageService(uploadsPath, baseUrl, logger);
});

// Register file access token service (for secure URL tokens on browser-initiated file requests)
builder.Services.AddSingleton<IFileAccessTokenService>(sp =>
{
    // Use JWT secret as the signing key for file access tokens
    var jwtSecret = builder.Configuration["JwtSettings:SecretKey"]
        ?? throw new InvalidOperationException("JwtSettings:SecretKey configuration is required for file access tokens");
    var logger = sp.GetRequiredService<ILogger<FileAccessTokenService>>();
    return new FileAccessTokenService(jwtSecret, logger);
});

// Register file URL service (consolidates token generation + URL building for all file types)
builder.Services.AddScoped<IFileUrlService, FileUrlService>();

// Phase 4 chunk 4.A — generic user-audit-log writer.
builder.Services.AddScoped<Famick.HomeManagement.Core.Interfaces.IUserAuditLogger,
    Famick.HomeManagement.Infrastructure.Services.UserAuditLogger>();
// Phase 4 chunk 4.D — local-server change-detection resolver for LoginResponse.
builder.Services.AddScoped<Famick.HomeManagement.Core.Interfaces.ILocalServerResolver,
    Famick.HomeManagement.Infrastructure.Services.LocalServerResolver>();

// Configure QuestPDF license (required since v2024.3.0)
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

// Register QR code and label sheet services (for storage bin labels)
builder.Services.AddScoped<Famick.HomeManagement.Web.Shared.Services.QrCodeService>();
builder.Services.AddScoped<Famick.HomeManagement.Web.Shared.Services.LabelSheetService>();

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Famick.HomeManagement.Core.Validators.ProductGroups.CreateProductGroupRequestValidator>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Add Swagger/OpenAPI for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "Famick Home Management API",
        Version = "v1",
        Description = "Self-hosted home management API"
    });

    // JWT Bearer Authentication
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(_ => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", null),
            new List<string>()
        }
    });

    // Include XML comments if file exists
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

builder.Services.AddHttpClient();

// Register Transfer to Cloud services
builder.Services.AddDbContext<Famick.HomeManagement.Web.Data.TransferDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient("CloudApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TransferToCloud:CloudUrl"] ?? "https://app.famick.com");
    client.Timeout = TimeSpan.FromSeconds(30);
});

// AuthProxy pairing — outbound HTTP to auth.famick.com so the admin can
// pair this home server with the cloud auth service. BaseUrl is config-
// driven; default targets the live Railway deploy.
builder.Services.AddHttpClient(Famick.HomeManagement.Infrastructure.Services.AuthProxyPairingService.HttpClientName, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AuthProxy:BaseUrl"] ?? "https://famick-auth.up.railway.app");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<Famick.HomeManagement.Core.Interfaces.IAuthProxyPairingService,
    Famick.HomeManagement.Infrastructure.Services.AuthProxyPairingService>();

// Step 6 — per-user cloud-login opt-in. Scoped because it touches the
// scoped DbContext + ITenantProvider; the singleton ITunnelSender is
// safely injectable into a scoped service.
builder.Services.AddScoped<Famick.HomeManagement.Core.Interfaces.ICloudLoginOptInService,
    Famick.HomeManagement.Infrastructure.Services.CloudLoginOptInService>();

// Outbound tunnel — Step 5 of the proxied-mode plan. Hosted service
// owns the connection lifecycle (polls for pairing config, exponential-
// backoff reconnect). TunnelHostedService doubles as the singleton
// ITunnelSender so Step 6's opt-in service can push USER_REGISTER /
// USER_UNREGISTER frames without holding a direct reference to the
// client.
// Singleton because it holds no per-request state — IHttpClientFactory
// is itself singleton, and TunnelHostedService (also singleton) needs
// to inject this. Don't scope it.
builder.Services.AddSingleton<Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.ITunnelRequestDispatcher,
    Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.TunnelRequestDispatcher>();
builder.Services.AddHttpClient(
    Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.TunnelRequestDispatcher.HttpClientName)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Loopback dispatch can hit https://localhost with the dev cert;
        // bypass validation for the loopback client only.
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
    });
builder.Services.AddSingleton<Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.TunnelHostedService>();
builder.Services.AddSingleton<Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.ITunnelSender>(sp =>
    sp.GetRequiredService<Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.TunnelHostedService>());
// Only register as a hosted service in web mode. Job-runner mode (invoked
// per scheduler tick) and any future short-lived CLI mode must NOT compete
// for the AuthProxy tunnel slot — TunnelRegistry evicts the prior tunnel
// when a new one registers under the same HomeServerId, which manifests as
// a 1s flapping reconnect loop when web + scheduler both hold a tunnel.
if (!isJobMode)
{
    builder.Services.AddHostedService(sp =>
        sp.GetRequiredService<Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.TunnelHostedService>());
}

builder.Services.AddSingleton<Famick.HomeManagement.Web.Services.ICloudTransferService,
    Famick.HomeManagement.Web.Services.CloudTransferService>();
builder.Services.AddSingleton<IFeatureManager, Famick.HomeManagement.Core.Services.FeatureManager>();

// Register IJob implementations (run via `run-job <name>` CLI; scheduled externally
// by docker-compose supercronic / AWS EventBridge)
builder.Services.AddJobs(builder.Configuration);

// ASP.NET Core Data Protection — persist keys to disk so antiforgery tokens,
// cookies, and anything else encrypted with the DPAPI survive container
// restarts. Without this, the framework falls back to ephemeral keys (the
// previous docker mount at /root/.aspnet/DataProtection-Keys was unreachable
// from the non-root `app` user, so every restart silently invalidated every
// signed cookie). SetApplicationName keeps keys isolated across deployments.
var dataProtectionPath = StoragePaths.ResolveDataProtectionPath(
    builder.Configuration, builder.Environment.ContentRootPath, storageRoot);
Directory.CreateDirectory(dataProtectionPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath))
    .SetApplicationName("Famick.HomeManagement");

// Build the application
var app = builder.Build();

await app.ConfigureInfrastructure(builder.Configuration);

// Job-runner mode: resolve the requested job, run it, and exit. Skips the
// web middleware pipeline and Kestrel.
if (isJobMode)
{
    return await Famick.HomeManagement.Jobs.JobRunner.RunAsync(app.Services, jobKey!, CancellationToken.None);
}

// Auto-migrate transfer tracking tables
using (var migrationScope = app.Services.CreateScope())
{
    var transferDb = migrationScope.ServiceProvider.GetRequiredService<Famick.HomeManagement.Web.Data.TransferDbContext>();
    await transferDb.Database.MigrateAsync();
}


// Configure the HTTP request pipeline

// Forwarded headers must be first for correct IP/protocol detection behind reverse proxy
app.UseForwardedHeaders();

// Global exception handling - must be early to catch all exceptions
app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Famick Home Management API v1");
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Force no-cache on Blazor WASM SPA shell HTML so the browser never serves a
// stale index.html after a deploy. Without this, a cached pre-deploy
// index.html keeps referring to old blazor.boot.json + assemblies even though
// the runtime expects new component contracts — symptom is "Unable to set
// property 'OnClick' on object of type 'MudBlazor.MudNavLink'" type
// InvalidCastException at component render that only a hard refresh clears.
// _framework/* assets already revalidate via integrity hashes; this just
// closes the loop on the SPA shell itself, which is served by both
// UseStaticFiles (direct /index.html) and MapFallbackToFile (SPA routes).
app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        var ct = context.Response.ContentType;
        if (ct != null && ct.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
        }
        return Task.CompletedTask;
    });
    await next();
});

// Blazor WASM hosting
app.UseBlazorFrameworkFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Disable caching for locale JSON files to ensure updates are picked up
        if (ctx.File.Name.EndsWith(".json") &&
            ctx.Context.Request.Path.StartsWithSegments("/_content") &&
            ctx.Context.Request.Path.Value?.Contains("/locales/") == true)
        {
            ctx.Context.Response.Headers.CacheControl = "no-cache, no-store";
        }
    }
});

// IP rate limiting - before routing
app.UseIpRateLimiting();

app.UseRouting();

// Tenant resolution middleware (for fixed tenant in self-hosted mode)
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();

// Phase 1 — destination-side JWT revocation. Runs immediately after AuthN so a
// stale-iat token is rejected before any flow-specific middleware (must_change_password,
// must_accept_terms) can let it through their allow-lists.
app.UseMiddleware<Famick.HomeManagement.Web.Shared.Middleware.JwtMinIatMiddleware>();

// Phase 2 — must-* gates wired into self-hosted for parity with cloud. The
// claims (must_change_password, must_accept_terms) are still set the same way
// in TokenService; this just ensures self-hosted enforces them server-side.
app.UseMiddleware<Famick.HomeManagement.Web.Shared.Middleware.MustChangePasswordMiddleware>();
app.UseMiddleware<Famick.HomeManagement.Web.Shared.Middleware.MustAcceptTermsMiddleware>();

app.UseAuthorization();

// Map health check endpoint with version info
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        var versionService = context.RequestServices.GetRequiredService<IVersionService>();
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            version = versionService.Version,
            informationalVersion = versionService.InformationalVersion,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});

// Map API controllers
app.MapControllers();

// Fallback to Blazor WASM for SPA routing
// This must be after MapControllers so API routes take precedence
app.MapFallbackToFile("index.html");

try
{
    Log.Information("Starting Famick Home Management application");
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

// Make Program class accessible to integration tests
public partial class Program { }
