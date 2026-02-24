using AspNetCoreRateLimit;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Middleware;
using Famick.HomeManagement.Web.Services;
using Famick.HomeManagement.Web.Cli;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Famick.HomeManagement.Infrastructure;
using Famick.HomeManagement.Web.Shared;
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

var builder = WebApplication.CreateBuilder(args);

// Add optional configuration from mounted volume (for Docker deployments)
// This allows users to override settings without rebuilding the image
var configPath = Path.Combine(builder.Environment.ContentRootPath, "config", "appsettings.json");
if (File.Exists(configPath))
{
    builder.Configuration.AddJsonFile(configPath, optional: true, reloadOnChange: true);
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/homemanagement-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllersWithViews();

// Add API controllers with JSON options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// Configure IP rate limiting
builder.Services.AddMemoryCache();
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
builder.Services.AddCore(builder.Configuration);

// Configure app store links for mobile deep linking
builder.Services.Configure<AppStoreLinksSettings>(
    builder.Configuration.GetSection("AppStoreLinks"));

// Register HttpContextAccessor for tenant resolution from HTTP context
builder.Services.AddHttpContextAccessor();

// Register TenantProvider (Fixed Tenant for self-hosted)
var fixedTenantId = builder.Configuration.GetValue<Guid>("FixedTenantId");
if (fixedTenantId == Guid.Empty)
{
    fixedTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
}
builder.Services.AddScoped<ITenantProvider>(sp =>
    new FixedTenantProvider(fixedTenantId, sp.GetRequiredService<IHttpContextAccessor>(), sp.GetRequiredService<ILogger<FixedTenantProvider>>()));

// Configure Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero,
        NameClaimType = "sub",  // Use "sub" claim as the user identifier
        RoleClaimType = "role"  // Match short claim name when MapInboundClaims is false
    };
});

// Configure authorization policies for role-based access
builder.Services.AddAuthorization(AuthorizationPolicies.Configure);

// Register file storage service (for product images and equipment documents)
// Files are stored outside wwwroot to prevent direct access - served through authenticated API endpoints
builder.Services.AddSingleton<IFileStorageService>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var logger = sp.GetRequiredService<ILogger<LocalFileStorageService>>();
    var baseUrl = builder.Configuration["BaseUrl"] ?? "";
    // Use ContentRootPath, not WebRootPath - files are served through API, not static files
    return new LocalFileStorageService(env.ContentRootPath, baseUrl, logger);
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

// Register QR code and label sheet services (for storage bin labels)
builder.Services.AddScoped<Famick.HomeManagement.Web.Shared.Services.QrCodeService>();
builder.Services.AddScoped<Famick.HomeManagement.Web.Shared.Services.LabelSheetService>();

// Add AutoMapper
builder.Services.AddAutoMapper(typeof(Famick.HomeManagement.Core.Mapping.ProductGroupMappingProfile).Assembly);

// Add FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Famick.HomeManagement.Core.Validators.ProductGroups.CreateProductGroupRequestValidator>();

// Add health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);

// Add Swagger/OpenAPI for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Famick Home Management API",
        Version = "v1",
        Description = "Self-hosted home management API"
    });

    // JWT Bearer Authentication
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
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
builder.Services.AddSingleton<Famick.HomeManagement.Web.Services.ICloudTransferService,
    Famick.HomeManagement.Web.Services.CloudTransferService>();

// Add notification background service
builder.Services.AddHostedService<NotificationBackgroundService>();

// Add external calendar sync background service
builder.Services.AddHostedService<ExternalCalendarSyncBackgroundService>();

// Add calendar reminder background service (5-minute polling)
builder.Services.AddHostedService<CalendarReminderBackgroundService>();

// Build the application
var app = builder.Build();

await app.ConfigureInfrastructure(builder.Configuration);

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
