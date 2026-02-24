using Famick.HomeManagement.Core.Interfaces.Plugins;

namespace Famick.HomeManagement.Core.DTOs.StoreIntegrations;

/// <summary>
/// Information about an available store integration plugin
/// </summary>
public class StoreIntegrationPluginInfo
{
    /// <summary>
    /// Plugin identifier (e.g., "kroger")
    /// </summary>
    public string PluginId { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name (e.g., "Kroger")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Whether the plugin is properly configured and available
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// Plugin capabilities - indicates which features are supported
    /// </summary>
    public StoreIntegrationCapabilities? Capabilities { get; set; }

    /// <summary>
    /// Whether the current tenant has a valid OAuth connection to this plugin.
    /// All stores using this plugin share the same token.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Whether the OAuth token refresh has failed and re-authentication is required.
    /// When true, the user must go through the OAuth flow again.
    /// </summary>
    public bool RequiresReauth { get; set; }
}
