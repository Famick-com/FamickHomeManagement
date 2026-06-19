using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions.StoreIntegration;

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
    /// Whether the plugin is usable for its client-credentials features
    /// (store search, product price/availability). True when the plugin is
    /// configured/available; these features need no user OAuth link.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Whether this plugin offers a user OAuth "link shopping cart" action
    /// (implements IOAuthClientAuthentication and supports a cart).
    /// </summary>
    public bool SupportsCartLink { get; set; }

    /// <summary>
    /// Whether the current tenant has completed the user OAuth link for this
    /// plugin (a valid token exists), enabling cart features.
    /// </summary>
    public bool CartLinked { get; set; }

    /// <summary>
    /// Whether the OAuth token refresh has failed and re-authentication is required.
    /// When true, the user must go through the OAuth flow again to use cart features.
    /// </summary>
    public bool RequiresReauth { get; set; }
}
