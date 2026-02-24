using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Core;

public static class CoreStartup
{
    public static IServiceCollection AddCore(this IServiceCollection services, IConfiguration configuration)
    {
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