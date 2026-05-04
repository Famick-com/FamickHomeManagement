using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Famick.HomeManagement.FeatureFlags;

public static class StartupExtensions
{
    /// <summary>
    /// Registers Microsoft.FeatureManagement and the <see cref="IFeatureFlagService"/>
    /// wrapper. Flag values are read from the <c>FeatureManagement</c> configuration
    /// section (appsettings.json or env vars like <c>FeatureManagement__step_up_enabled=true</c>).
    /// Unconfigured flags default to <c>false</c>.
    /// </summary>
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddFeatureManagement(configuration.GetSection("FeatureManagement"));
        services.AddSingleton<IFeatureFlagService, FeatureFlagService>();
        return services;
    }
}
