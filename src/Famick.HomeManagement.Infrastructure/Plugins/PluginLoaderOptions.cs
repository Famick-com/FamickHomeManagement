namespace Famick.HomeManagement.Infrastructure.Plugins;

/// <summary>
/// Configuration options for the plugin loader
/// </summary>
public class PluginLoaderOptions
{
    /// <summary>
    /// Path to the plugins folder (default: "plugins")
    /// </summary>
    public string PluginsPath { get; set; } = "plugins";

    /// <summary>
    /// Whether to load plugins on application startup
    /// </summary>
    public bool LoadPluginsOnStartup { get; set; } = true;
}
