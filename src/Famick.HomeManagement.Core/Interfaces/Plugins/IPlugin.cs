using System.Text.Json;

namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Base interface for all plugins
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Unique identifier for this plugin (e.g., "usda", "openfoodfacts")
    /// Also used as the key in plugins/config.json
    /// </summary>
    string PluginId { get; }

    /// <summary>
    /// Human-readable display name (e.g., "USDA FoodData Central")
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Plugin version string
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Whether the plugin is currently available (initialized and configured)
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Attribution and licensing requirements for data from this plugin.
    /// Null if no specific attribution is required.
    /// </summary>
    PluginAttribution? Attribution { get; }

    /// <summary>
    /// Initialize the plugin with its configuration section from plugins/config.json
    /// Each plugin defines its own configuration schema
    /// </summary>
    /// <param name="pluginConfig">The plugin's configuration section as a JsonElement, or null if not configured</param>
    /// <param name="ct">Cancellation token</param>
    Task InitAsync(JsonElement? pluginConfig, CancellationToken ct = default);
}
