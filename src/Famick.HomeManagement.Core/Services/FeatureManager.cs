using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// Configuration-based implementation of feature management.
/// Reads feature flags from appsettings.json or environment variables.
/// </summary>
public class FeatureManager : IFeatureManager
{
    private readonly IConfiguration _configuration;
    private const string FeaturesSection = "Features";
    private const string CloudIntegrationsSection = "CloudIntegrations";

    public FeatureManager(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Checks if a feature is enabled by reading from the Features configuration section.
    /// </summary>
    /// <param name="featureName">The name of the feature</param>
    /// <returns>True if enabled (defaults to true if not configured)</returns>
    public bool IsEnabled(string featureName)
    {
        if (string.IsNullOrWhiteSpace(featureName))
            throw new ArgumentException("Feature name cannot be null or empty", nameof(featureName));

        var featureKey = $"{FeaturesSection}:{featureName}";
        var value = _configuration.GetValue<bool?>(featureKey);

        // Default to true if not explicitly configured
        return value ?? true;
    }

    /// <summary>
    /// Checks if a cloud integration is enabled.
    /// First checks if CloudIntegrations feature is enabled, then checks specific integration.
    /// </summary>
    /// <param name="integrationName">The name of the integration</param>
    /// <returns>True if both CloudIntegrations and the specific integration are enabled</returns>
    public bool IsCloudIntegrationEnabled(string integrationName)
    {
        if (string.IsNullOrWhiteSpace(integrationName))
            throw new ArgumentException("Integration name cannot be null or empty", nameof(integrationName));

        // First check if cloud integrations are enabled at all
        if (!IsEnabled(FeatureNames.CloudIntegrations))
            return false;

        // Then check if the specific integration is enabled
        var integrationKey = $"{CloudIntegrationsSection}:{integrationName}";
        var value = _configuration.GetValue<bool?>(integrationKey);

        // Default to false for specific integrations (opt-in)
        return value ?? false;
    }
}
