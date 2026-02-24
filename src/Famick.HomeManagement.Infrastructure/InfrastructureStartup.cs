using Microsoft.EntityFrameworkCore;
using System.ComponentModel.Design;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Fido2NetLib;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

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


        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<ISetupService, SetupService>();

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
        services.AddScoped<ICalendarAvailabilityService, CalendarAvailabilityService>();

        // Register notification services
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEvaluator, ExpiryAndStockEvaluator>();
        services.AddScoped<INotificationEvaluator, TaskSummaryEvaluator>();
        services.AddScoped<INotificationEvaluator, CalendarEventEvaluator>();
        services.AddScoped<INotificationDispatcher, InAppNotificationDispatcher>();
        services.AddScoped<INotificationDispatcher, EmailNotificationDispatcher>();
        services.AddSingleton<IDistributedLockService, NoOpDistributedLockService>();

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

        // Configure Geoapify address normalization service
        services.Configure<GeoapifyOptions>(configuration.GetSection(GeoapifyOptions.SectionName));
        services.AddHttpClient<IAddressNormalizationService, GeoapifyAddressService>();

        // Configure plugin system
        services.Configure<Plugins.PluginLoaderOptions>(options =>
        {
            options.PluginsPath = Path.Combine(environment.ContentRootPath, "plugins");
            options.LoadPluginsOnStartup = true;
        });


        // Register built-in plugins (order matters for pipeline - first registered runs first)
        services.AddSingleton<Core.Interfaces.Plugins.IPlugin,
            Plugins.Usda.UsdaFoodDataPlugin>();
        services.AddSingleton<Core.Interfaces.Plugins.IPlugin,
            Plugins.OpenFoodFacts.OpenFoodFactsPlugin>();
        // Register plugin loader and lookup service
        services.AddSingleton<Core.Interfaces.Plugins.IPluginLoader,
            Plugins.PluginLoader>();
        services.AddScoped<IProductLookupService,
            ProductLookupService>();

        // Register store integration plugin system
        services.AddScoped<IStoreIntegrationService, StoreIntegrationService>();


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
        }

        // Load plugins on startup
        var pluginLoader = app.Services.GetRequiredService<Core.Interfaces.Plugins.IPluginLoader>();
        await pluginLoader.LoadPluginsAsync();
    }
}
