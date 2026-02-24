namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Manages feature flags for the application.
/// Allows different features to be enabled/disabled between self-hosted and cloud deployments.
/// </summary>
public interface IFeatureManager
{
    /// <summary>
    /// Checks if a specific feature is enabled.
    /// </summary>
    /// <param name="featureName">The name of the feature to check (use constants from FeatureNames class)</param>
    /// <returns>True if the feature is enabled, false otherwise</returns>
    bool IsEnabled(string featureName);

    /// <summary>
    /// Checks if a specific cloud integration is enabled.
    /// </summary>
    /// <param name="integrationName">The name of the integration (e.g., "AmazonFresh", "InstaCart")</param>
    /// <returns>True if the integration is enabled, false otherwise</returns>
    bool IsCloudIntegrationEnabled(string integrationName);
}

/// <summary>
/// Constants for feature names used throughout the application.
/// </summary>
public static class FeatureNames
{
    /// <summary>
    /// Stock/inventory management feature
    /// </summary>
    public const string Stock = "Stock";

    /// <summary>
    /// Recipe and meal planning feature
    /// </summary>
    public const string Recipes = "Recipes";

    /// <summary>
    /// Chore tracking and scheduling feature
    /// </summary>
    public const string Chores = "Chores";

    /// <summary>
    /// Task management feature
    /// </summary>
    public const string Tasks = "Tasks";

    /// <summary>
    /// Cloud integrations (store integrations, external APIs, etc.)
    /// Available only in cloud version or as paid add-on for self-hosted
    /// </summary>
    public const string CloudIntegrations = "CloudIntegrations";

    /// <summary>
    /// Store integrations (Amazon Fresh, InstaCart, etc.)
    /// Subset of CloudIntegrations
    /// </summary>
    public const string StoreIntegrations = "StoreIntegrations";

    /// <summary>
    /// Premium barcode lookup APIs
    /// Self-hosted uses free APIs, cloud uses premium APIs
    /// </summary>
    public const string PremiumBarcodeLookup = "PremiumBarcodeLookup";

    /// <summary>
    /// Mobile native applications
    /// Self-hosted has PWA only, cloud has native apps
    /// </summary>
    public const string NativeMobileApps = "NativeMobileApps";

    /// <summary>
    /// Transfer to Cloud feature
    /// Allows self-hosted users to transfer their data to the cloud version
    /// </summary>
    public const string TransferToCloud = "TransferToCloud";
}
