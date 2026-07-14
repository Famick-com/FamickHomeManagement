using Microsoft.EntityFrameworkCore;
using System.ComponentModel.Design;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Platform;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging;
using Famick.HomeManagement.Messaging.Interfaces;
using Fido2NetLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Famick.HomeManagement.Plugin.Abstractions;

namespace Famick.HomeManagement.Infrastructure;

public static class InfrastructureStartup
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Configure database context
        services.AddDbContext<HomeManagementDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly("Famick.HomeManagement.Infrastructure");
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        });

        // DbContext factory for parallel query execution (e.g., parent product search)
        // Use Scoped lifetime to match the DbContextOptions registered by AddDbContext above
        services.AddDbContextFactory<HomeManagementDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null);
            });
        }, ServiceLifetime.Scoped);


        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ISetupService, SetupService>();

        // First-class server platform, resolved once from IsMultiTenantEnabled +
        // HaIngress:Enabled. Lazy factory so it doesn't depend on the order
        // IMultiTenancyOptions is registered (it's set in each host's Program.cs).
        services.AddSingleton<IPlatformInfo>(sp =>
        {
            var multiTenancyOptions = sp.GetRequiredService<IMultiTenancyOptions>();
            var haIngress = configuration.GetSection(HaIngressSettings.SectionName).Get<HaIngressSettings>();
            return new PlatformInfo(
                PlatformResolver.Resolve(
                    multiTenancyOptions.IsMultiTenantEnabled,
                    haIngress?.Enabled ?? false));
        });

        // Phase 1 — destination-side JWT revocation. Default registration is the
        // Postgres-only impl; the cloud project replaces it with a Redis-cached
        // decorator over this same inner service.
        services.AddScoped<IJwtMinIatService, JwtMinIatService>();

        // Phase 1 — per-user advisory locks (password change + refresh-token rotation
        // critical sections). Default Postgres-only impl; cloud project replaces with
        // Redis distributed-lock impl for cross-instance coordination.
        services.AddScoped<IUserAdvisoryLockService, PostgresUserAdvisoryLockService>();

        // Register email service based on configuration
        var emailSettings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>();
        if (emailSettings?.Provider == EmailProvider.AwsSes)
        {
            services.AddScoped<IEmailService, AwsSesEmailService>();
        }
        else
        {
            services.AddScoped<IEmailService, SmtpEmailService>();
        }

        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IRegistrationService, RegistrationService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IUserProfileService, UserProfileService>();

        // Register data seeder
        services.AddScoped<DataSeeder>();

        // Register business services (from homemanagement-shared)
        services.AddScoped<IProductGroupService, ProductGroupService>();
        services.AddScoped<IShoppingLocationService, ShoppingLocationService>();
        services.AddScoped<IShoppingListService, ShoppingListService>();
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<IChoreService, ChoreService>();
        services.AddScoped<IProductSearchService, ProductSearchService>();
        services.AddScoped<IProductsService, ProductsService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IHomeService, HomeService>();
        services.AddScoped<IEquipmentService, EquipmentService>();
        services.AddScoped<IStorageBinService, StorageBinService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ITodoItemService, TodoItemService>();
        services.AddScoped<IVehicleService, VehicleService>();
        services.AddScoped<IWizardService, WizardService>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddHttpClient<IExternalCalendarService, ExternalCalendarService>();
        services.AddScoped<ICalendarFeedService, CalendarFeedService>();
        services.AddScoped<IContactFeedService, ContactFeedService>();
        services.AddScoped<ICalendarAvailabilityService, CalendarAvailabilityService>();

        // Meal planner services
        services.AddScoped<IMealTypeService, MealTypeService>();
        services.AddScoped<IMealService, MealService>();
        services.AddScoped<IMealPlanService, MealPlanService>();
        services.AddScoped<IDietaryProfileService, DietaryProfileService>();
        services.AddScoped<IProductAllergenService, ProductAllergenService>();
        services.AddScoped<IAllergenWarningService, AllergenWarningService>();
        services.AddScoped<IMealPlannerOnboardingService, MealPlannerOnboardingService>();
        services.AddScoped<IProductOnboardingService, ProductOnboardingService>();
        services.AddScoped<MasterProductSeeder>();
        services.AddSingleton<IMasterProductImageResolver>(sp =>
        {
            var fileStorage = sp.GetRequiredService<IFileStorageService>();
            var baseUrl = configuration["BaseUrl"] ?? "";
            return new MasterProductImageResolver(fileStorage, baseUrl);
        });

        // Register no-op contact sync push service (cloud overrides with real implementation)
        services.AddSingleton<IContactSyncPushService, NullContactSyncPushService>();

        // Register no-op reminder sync push service (cloud overrides with real silent-push implementation)
        services.AddSingleton<IReminderSyncPushService, NullReminderSyncPushService>();


        // Register notification services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEvaluator, ExpiryEvaluator>();
        services.AddScoped<INotificationEvaluator, LowStockEvaluator>();
        services.AddScoped<INotificationEvaluator, TaskSummaryEvaluator>();
        services.AddScoped<INotificationEvaluator, CalendarEventEvaluator>();
        // Future-dated reminder feed for the mobile offline notification engine (self-hosted mode).
        services.AddScoped<IUpcomingReminderService, UpcomingReminderService>();
        services.AddSingleton<IDistributedLockService, NoOpDistributedLockService>();

        // Register unified messaging service
        services.AddMessaging(configuration);
        services.AddScoped<IMessageRecipientResolver, MessageRecipientResolver>();

        // Register unsubscribe token service (same pattern as FileAccessTokenService)
        var jwtSecretKey = configuration["JwtSettings:SecretKey"] ?? "";
        services.AddSingleton<IUnsubscribeTokenService>(sp =>
            new UnsubscribeTokenService(
                jwtSecretKey,
                sp.GetRequiredService<ILogger<UnsubscribeTokenService>>()));

        // Configure External Authentication
        services.Configure<ExternalAuthSettings>(configuration.GetSection("ExternalAuth"));
        services.AddScoped<IExternalAuthService, ExternalAuthService>();

        // Configure Passkey/WebAuthn authentication
        var passkeySettings = configuration.GetSection("ExternalAuth:Passkey").Get<PasskeySettings>();
        if (passkeySettings?.IsConfigured == true)
        {
            var fido2Config = new Fido2Configuration
            {
                ServerDomain = passkeySettings.RelyingPartyId,
                ServerName = passkeySettings.RelyingPartyName,
                Origins = passkeySettings.Origins?.ToHashSet() ?? new HashSet<string>()
            };
            services.AddSingleton(fido2Config);
            services.AddSingleton<IFido2, Fido2>(sp =>
                new Fido2(fido2Config, sp.GetService<IMetadataService>()));
        }
        else
        {
            // Register a null Fido2 service when not configured
            services.AddSingleton<IFido2>(sp =>
                new Fido2(new Fido2Configuration
                {
                    ServerDomain = "localhost",
                    ServerName = "HomeManagement",
                    Origins = new HashSet<string> { "https://localhost" }
                }));
        }
        services.AddScoped<IPasskeyService, PasskeyService>();

        services.AddScoped<IAddressService, AddressService>();

        // Geoapify: used for address normalization (existing) AND as the default
        // autocomplete provider for self-hosted deployments.
        services.Configure<GeoapifyOptions>(configuration.GetSection(GeoapifyOptions.SectionName));
        services.AddHttpClient<IAddressNormalizationService, GeoapifyAddressService>();

        // Smarty options (only used when explicitly selected as the provider).
        services.Configure<SmartyOptions>(configuration.GetSection(SmartyOptions.SectionName));

        // Autocomplete provider selection. Cloud sets
        // `AddressAutocomplete__Provider=Smarty`; self-hosted defaults to
        // Geoapify (request-limited free tier, single API key).
        services.AddMemoryCache();
        services.AddSingleton<IAddressSuggestionCache, AddressSuggestionCache>();

        var autocompleteProvider = configuration["AddressAutocomplete:Provider"] ?? "Geoapify";
        if (autocompleteProvider.Equals("Smarty", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAddressAutocompleteProvider, SmartyAddressAutocompleteProvider>();
            Log.Information("Address autocomplete provider: Smarty (US Autocomplete Pro)");
        }
        else
        {
            services.AddHttpClient<IAddressAutocompleteProvider, GeoapifyAddressAutocompleteProvider>();
            Log.Information("Address autocomplete provider: Geoapify (default for self-hosted)");
        }

        // Address canonicalizer for NormalizedHash dedup. PassThrough is the
        // default (no extra container needed); Libpostal opts in to a
        // libpostal-rest sidecar that collapses format variations
        // ("St"/"Street", "N"/"North") so hand-entered addresses dedupe
        // against each other.
        services.Configure<LibpostalOptions>(configuration.GetSection(LibpostalOptions.SectionName));
        var canonicalizerProvider = configuration["AddressCanonicalizer:Provider"] ?? "None";
        if (canonicalizerProvider.Equals("Libpostal", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAddressCanonicalizer, LibpostalRestCanonicalizer>(client =>
            {
                var timeoutSeconds = configuration.GetValue<int?>("Libpostal:TimeoutSeconds") ?? 5;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            Log.Information("Address canonicalizer: libpostal-rest sidecar");
        }
        else
        {
            services.AddSingleton<IAddressCanonicalizer, PassThroughAddressCanonicalizer>();
            Log.Information("Address canonicalizer: pass-through (no libpostal)");
        }
        services.AddScoped<IAddressHasher, AddressHasher>();

        // Storage:Path is the single root for all operator-mutable data — plugins,
        // server-config overlay, Data Protection keys, uploads. Each derived path
        // is individually overridable via its own setting. Default is "data" under
        // ContentRootPath (so docker's /app/data and dev's local_config/ both work
        // by changing a single Storage:Path value). See Core.Configuration.StoragePaths.
        var storageRoot = StoragePaths.ResolveStorageRoot(configuration, environment.ContentRootPath);

        // Configure plugin system. The loader and the IPluginConfigService below
        // share this path so they always agree on where config.json lives.
        var pluginsPath = StoragePaths.ResolvePluginsPath(configuration, environment.ContentRootPath, storageRoot);
        services.Configure<Plugins.PluginLoaderOptions>(options =>
        {
            options.PluginsPath = pluginsPath;
            options.LoadPluginsOnStartup = true;
        });

        // Service that reads/writes the self-hosted server-config.json overlay.
        // Singleton: the file path is fixed for the process and the service holds
        // a write-mutex to serialize updates.
        var serverConfigPath = StoragePaths.ResolveServerConfigPath(configuration, environment.ContentRootPath, storageRoot);
        services.AddSingleton<IServerConfigService>(sp =>
            new ServerConfigService(
                serverConfigPath,
                sp.GetRequiredService<ILogger<ServerConfigService>>()));

        // Companion service for the admin Plugins page — reads/writes
        // plugins/config.json and scans the plugins folder for un-registered DLLs.
        // Uses the same Plugins:Path resolution as the loader so they always agree.
        services.AddSingleton<IPluginConfigService>(sp =>
            new PluginConfigService(
                pluginsPath,
                sp.GetServices<IPlugin>(),
                sp.GetRequiredService<Core.Interfaces.Plugins.IPluginLoader>(),
                sp.GetRequiredService<ILogger<PluginConfigService>>()));


        // Register built-in plugins (order matters for pipeline - first registered runs first)
        services.AddSingleton<IPlugin,
            Plugins.Usda.UsdaFoodDataPlugin>();
        services.AddSingleton<IPlugin,
            Plugins.OpenFoodFacts.OpenFoodFactsPlugin>();
        // Register plugin loader and lookup service
        services.AddSingleton<Core.Interfaces.Plugins.IPluginLoader,
            Plugins.PluginLoader>();
        services.AddScoped<IProductLookupService,
            ProductLookupService>();

        // Register store integration plugin system
        services.AddScoped<IStoreIntegrationService, StoreIntegrationService>();

        // Register message forwarding handler (stub — implement when ingestion endpoint is ready)
        services.AddScoped<Core.Messaging.IMessageHandler, MessageForwardingHandler>();

        return services;
    }

    public static async Task ConfigureInfrastructure(this IHost app, IConfiguration configuration)
    {
        // Apply pending migrations on startup (configurable, default: true for self-hosted)
        var autoMigrate = configuration.GetValue<bool>("Database:AutoMigrate", true);
        if (autoMigrate)
        {
            // Create a base HomeManagementDbContext directly for migrations.
            // This is necessary because DI may resolve a derived context (e.g. CloudHomeManagementDbContext),
            // but all migrations are decorated with [DbContext(typeof(HomeManagementDbContext))].
            // EF Core matches migrations by exact context type, not by inheritance.
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var optionsBuilder = new DbContextOptionsBuilder<HomeManagementDbContext>();
            optionsBuilder.UseNpgsql(connectionString, o => o.MigrationsAssembly("Famick.HomeManagement.Infrastructure"));
            using var migrationContext = new HomeManagementDbContext(optionsBuilder.Options);

            var pendingMigrations = await migrationContext.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                Log.Information("Applying {Count} pending database migration(s)...", pendingMigrations.Count());
                await migrationContext.Database.MigrateAsync();
                Log.Information("Database migrations applied successfully");
            }
        }

        // Seed default data for the fixed tenant
        using (var scope = app.Services.CreateScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
            var tenantProvider = scope.ServiceProvider.GetRequiredService<ITenantProvider>();
            if (tenantProvider.TenantId.HasValue)
            {
                // Ensure tenant record exists
                var tenantService = scope.ServiceProvider.GetRequiredService<ITenantService>();
                await tenantService.EnsureTenantExistsAsync(tenantProvider.TenantId.Value);

                await seeder.SeedDefaultDataAsync(tenantProvider.TenantId.Value);
            }

            // Seed default equipment document tags
            var equipmentService = scope.ServiceProvider.GetRequiredService<IEquipmentService>();
            await equipmentService.SeedDefaultTagsAsync();

            // Seed global master products and auto-link existing tenant products (idempotent)
            var masterProductSeeder = scope.ServiceProvider.GetRequiredService<MasterProductSeeder>();
            await masterProductSeeder.SeedAsync();
        }

        // Validate message templates on startup (fail-fast if any are missing)
        app.Services.ValidateMessagingTemplates();

        // Load plugins on startup
        var pluginLoader = app.Services.GetRequiredService<Core.Interfaces.Plugins.IPluginLoader>();
        await pluginLoader.LoadPluginsAsync();
    }
}
