using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Famick.HomeManagement.Core;

public static class CoreStartup
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
        // Register JWT signing key service (singleton - same key for entire app lifetime)
        // Skip if already registered (self-hosted Program.cs pre-registers to share with JWT middleware)
        services.TryAddSingleton<IJwtSigningKeyService, JwtSigningKeyService>();

        // Register authentication services
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();

        // Register version service
        services.AddSingleton<IVersionService, VersionService>();

        // Configure email settings
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));

        // Configure notification settings
        services.Configure<NotificationSettings>(configuration.GetSection(NotificationSettings.SectionName));

        // Configure calendar settings
        services.Configure<CalendarSettings>(configuration.GetSection(CalendarSettings.SectionName));

        return services;
    }
}