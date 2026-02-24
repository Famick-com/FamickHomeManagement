using System.Reflection;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Plugins;

/// <summary>
/// Plugin loader that reads configuration from plugins/config.json and loads plugins accordingly.
/// Built-in plugins are loaded by ID, external plugins are loaded from DLL files.
/// </summary>
public class PluginLoader : IPluginLoader
{
    private readonly ILogger<PluginLoader> _logger;
    private readonly PluginLoaderOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, IPlugin> _builtinPlugins;
    private List<IPlugin> _plugins = new();
    private List<PluginConfigEntry> _configurations = new();

    public PluginLoader(
        ILogger<PluginLoader> logger,
        IOptions<PluginLoaderOptions> options,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        IEnumerable<IPlugin> builtinPlugins)
    {
        _logger = logger;
        _options = options.Value;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _builtinPlugins = builtinPlugins.ToDictionary(p => p.PluginId, p => p);
    }

    public IReadOnlyList<IPlugin> Plugins => _plugins.AsReadOnly();

    IReadOnlyList<IPlugin> IPluginLoader.Plugins => Plugins;

    public IReadOnlyList<T> GetAvailablePlugins<T>() where T : IPlugin
    {
        // Returns plugins in config.json order (order they were loaded)
        return _plugins
            .Where(p => p.IsAvailable)
            .OfType<T>()
            .ToList()
            .AsReadOnly();
    }

    public T? GetPlugin<T>(string pluginId) where T : IPlugin
    {
        return _plugins
            .Where(p => p.IsAvailable)
            .OfType<T>()
            .FirstOrDefault(p => p.PluginId == pluginId);
    }

    public IReadOnlyList<PluginConfigEntry> GetPluginConfigurations()
    {
        return _configurations.AsReadOnly();
    }

    public async Task LoadPluginsAsync(CancellationToken ct = default)
    {
        _plugins.Clear();
        _configurations.Clear();

        var configPath = Path.Combine(_options.PluginsPath, "config.json");

        if (!File.Exists(configPath))
        {
            // No config file - auto-load all built-in plugins with default settings
            _logger.LogInformation(
                "Plugin configuration file not found at {Path}. Auto-loading {Count} built-in plugins.",
                configPath, _builtinPlugins.Count);

            await LoadBuiltinPluginsAsync(ct);
            return;
        }

        try
        {
            var configJson = await File.ReadAllTextAsync(configPath, ct);
            var configDoc = JsonDocument.Parse(configJson);

            if (!configDoc.RootElement.TryGetProperty("plugins", out var pluginsArray))
            {
                _logger.LogWarning("No 'plugins' array found in config.json");
                return;
            }

            foreach (var pluginElement in pluginsArray.EnumerateArray())
            {
                var entry = ParsePluginEntry(pluginElement);
                if (entry == null) continue;

                _configurations.Add(entry);

                if (!entry.Enabled)
                {
                    _logger.LogInformation("Plugin {PluginId} is disabled, skipping", entry.Id);
                    continue;
                }

                var plugin = await LoadPluginAsync(entry, ct);
                if (plugin != null)
                {
                    _plugins.Add(plugin);
                    _logger.LogInformation("Loaded plugin {PluginId} ({DisplayName}) v{Version}",
                        plugin.PluginId, plugin.DisplayName, plugin.Version);
                }
            }

            _logger.LogInformation("Loaded {Count} plugins", _plugins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugins from {Path}", configPath);
        }
    }

    /// <summary>
    /// Auto-load all built-in plugins when no config.json exists.
    /// This ensures product lookup works out of the box without manual configuration.
    /// </summary>
    private async Task LoadBuiltinPluginsAsync(CancellationToken ct)
    {
        foreach (var (pluginId, plugin) in _builtinPlugins)
        {
            try
            {
                // Create a default config entry
                var entry = new PluginConfigEntry
                {
                    Id = pluginId,
                    Enabled = true,
                    Builtin = true,
                    DisplayName = plugin.DisplayName
                };
                _configurations.Add(entry);

                // Initialize plugin with config overrides from environment variables
                var mergedConfig = MergeConfigurationOverrides(pluginId, null);
                await plugin.InitAsync(mergedConfig, ct);
                _plugins.Add(plugin);

                _logger.LogInformation("Auto-loaded built-in plugin {PluginId} ({DisplayName}) v{Version}",
                    plugin.PluginId, plugin.DisplayName, plugin.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-load built-in plugin {PluginId}", pluginId);
            }
        }

        _logger.LogInformation("Auto-loaded {Count} built-in plugins", _plugins.Count);
    }

    private PluginConfigEntry? ParsePluginEntry(JsonElement element)
    {
        try
        {
            var entry = new PluginConfigEntry
            {
                Id = element.GetProperty("id").GetString() ?? string.Empty,
                Enabled = element.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(),
                Builtin = element.TryGetProperty("builtin", out var builtin) && builtin.GetBoolean(),
                Assembly = element.TryGetProperty("assembly", out var assembly) ? assembly.GetString() : null,
                DisplayName = element.TryGetProperty("displayName", out var displayName)
                    ? displayName.GetString() ?? string.Empty
                    : string.Empty
            };

            if (element.TryGetProperty("config", out var config))
            {
                entry.Config = config.Clone();
            }

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse plugin configuration entry");
            return null;
        }
    }

    private async Task<IPlugin?> LoadPluginAsync(PluginConfigEntry entry, CancellationToken ct)
    {
        try
        {
            IPlugin? plugin;

            if (entry.Builtin)
            {
                // Load built-in plugin by ID
                if (!_builtinPlugins.TryGetValue(entry.Id, out plugin))
                {
                    _logger.LogWarning("Built-in plugin {PluginId} not found", entry.Id);
                    return null;
                }
            }
            else
            {
                // Load external plugin from DLL
                if (string.IsNullOrEmpty(entry.Assembly))
                {
                    _logger.LogWarning("External plugin {PluginId} has no assembly path", entry.Id);
                    return null;
                }

                plugin = LoadExternalPlugin(entry);
                if (plugin == null) return null;
            }

            // Initialize the plugin with its configuration, merged with env var overrides
            var mergedConfig = MergeConfigurationOverrides(entry.Id, entry.Config);
            await plugin.InitAsync(mergedConfig, ct);
            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load plugin {PluginId}", entry.Id);
            return null;
        }
    }

    /// <summary>
    /// Merge IConfiguration overrides (e.g. from environment variables) into the file-based plugin config.
    /// Convention: env var Plugins__kroger__clientSecret → IConfiguration key Plugins:kroger:clientSecret
    /// </summary>
    private JsonElement? MergeConfigurationOverrides(string pluginId, JsonElement? fileConfig)
    {
        var section = _configuration.GetSection($"Plugins:{pluginId}");
        var overrides = section.GetChildren().ToList();

        if (overrides.Count == 0)
        {
            return fileConfig;
        }

        // Start with existing config values or empty dictionary
        var configDict = new Dictionary<string, object>();

        if (fileConfig.HasValue && fileConfig.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in fileConfig.Value.EnumerateObject())
            {
                configDict[property.Name] = property.Value.Clone();
            }
        }

        // Overlay IConfiguration values (env vars take precedence)
        foreach (var child in overrides)
        {
            if (child.Value != null)
            {
                configDict[child.Key] = child.Value;
                _logger.LogDebug("Plugin {PluginId}: overriding config key '{Key}' from environment",
                    pluginId, child.Key);
            }
        }

        // Re-serialize to JsonElement
        var json = JsonSerializer.Serialize(configDict);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private IPlugin? LoadExternalPlugin(PluginConfigEntry entry)
    {
        var assemblyPath = Path.Combine(_options.PluginsPath, entry.Assembly!);

        if (!File.Exists(assemblyPath))
        {
            _logger.LogWarning("Plugin assembly not found: {Path}", assemblyPath);
            return null;
        }

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var pluginType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                _logger.LogWarning("No IPlugin implementation found in {Path}", assemblyPath);
                return null;
            }

            var plugin = (IPlugin?)Activator.CreateInstance(pluginType);
            if (plugin == null)
            {
                _logger.LogWarning("Failed to create instance of plugin type {Type}", pluginType.FullName);
                return null;
            }

            return plugin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load external plugin from {Path}", assemblyPath);
            return null;
        }
    }

}
