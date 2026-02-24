namespace Famick.HomeManagement.Core.Interfaces.Plugins;

/// <summary>
/// Service for loading and managing product lookup plugins
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// All loaded plugins (both built-in and external)
    /// </summary>
    IReadOnlyList<IPlugin> Plugins { get; }

    /// <summary>
    /// Get all enabled and available plugins in config.json order (pipeline execution order)
    /// </summary>
    IReadOnlyList<T> GetAvailablePlugins<T>() where T : IPlugin;

    /// <summary>
    /// Get a specific plugin by ID
    /// </summary>
    /// <param name="pluginId">Plugin identifier</param>
    /// <returns>The plugin, or null if not found</returns>
    T? GetPlugin<T>(string pluginId) where T : IPlugin;

    /// <summary>
    /// Load/reload all plugins from the configuration
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task LoadPluginsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get plugin configuration entries from config.json
    /// </summary>
    IReadOnlyList<PluginConfigEntry> GetPluginConfigurations();
}
