using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces.Plugins;
using Famick.HomeManagement.Plugin.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, IPlugin> _builtinPlugins;
    private List<IPlugin> _plugins = new();
    private List<PluginConfigEntry> _configurations = new();

    public PluginLoader(
        ILogger<PluginLoader> logger,
        IOptions<PluginLoaderOptions> options,
        IConfiguration configuration,
        IEnumerable<IPlugin> builtinPlugins)
    {
        _logger = logger;
        _options = options.Value;
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

            // Parse all entries and load built-in plugins immediately
            var externalEntries = new List<PluginConfigEntry>();

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

                if (entry.Builtin)
                {
                    // Load built-in plugins directly from injected instances
                    if (_builtinPlugins.TryGetValue(entry.Id, out var builtinPlugin))
                    {
                        var mergedConfig = MergeConfigurationOverrides(entry.Id, entry.Config);
                        await builtinPlugin.InitAsync(mergedConfig, ct);
                        _plugins.Add(builtinPlugin);
                        _logger.LogInformation("Loaded built-in plugin {PluginId} ({DisplayName}) v{Version}",
                            builtinPlugin.PluginId, builtinPlugin.DisplayName, builtinPlugin.Version);
                    }
                    else
                    {
                        _logger.LogWarning("Built-in plugin {PluginId} not found", entry.Id);
                    }
                }
                else
                {
                    externalEntries.Add(entry);
                }
            }

            // Three-phase external plugin loading
            if (externalEntries.Count > 0)
            {
                await LoadExternalPluginsAsync(externalEntries, ct);
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
                Type = element.TryGetProperty("type", out var type) ? type.GetString() : null,
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

    /// <summary>
    /// Three-phase external plugin loading:
    /// Phase 1: Resolve types and load assemblies via isolated AssemblyLoadContext
    /// Phase 2: Build DI container with IStartup registrations from plugin assemblies
    /// Phase 3: Resolve plugin instances from DI and initialize them
    /// </summary>
    private async Task LoadExternalPluginsAsync(List<PluginConfigEntry> entries, CancellationToken ct)
    {
        // Phase 1: Resolve types and load assemblies
        var resolved = new List<(PluginConfigEntry Entry, Type PluginType, Assembly Assembly, string AssemblyPath)>();

        foreach (var entry in entries)
        {
            var result = ResolveExternalPlugin(entry);
            if (result == null) continue;

            var (pluginType, assembly, assemblyPath) = result.Value;
            resolved.Add((entry, pluginType, assembly, assemblyPath));
        }

        if (resolved.Count == 0) return;

        // Phase 2: Build DI container with base services and IStartup registrations
        var services = new ServiceCollection();
        services.AddHttpClient();
        services.AddLogging();

        foreach (var (entry, pluginType, assembly, _) in resolved)
        {
            // Scan the assembly for IStartup implementations and invoke them
            try
            {
                var startupTypes = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface &&
                                typeof(IStartup).IsAssignableFrom(t));

                foreach (var startupType in startupTypes)
                {
                    if (Activator.CreateInstance(startupType) is IStartup startup)
                    {
                        startup.ConfigureServices(services);
                        _logger.LogDebug("IStartup {StartupType} registered services for plugin {PluginId}",
                            startupType.Name, entry.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IStartup scan failed for plugin {PluginId}", entry.Id);
            }

            // Register the plugin type if IStartup didn't already register it
            if (!services.Any(sd => sd.ServiceType == pluginType))
            {
                services.AddTransient(pluginType);
            }
        }

        var pluginServiceProvider = services.BuildServiceProvider();

        // Phase 3: Resolve plugin instances from DI and initialize
        foreach (var (entry, pluginType, _, _) in resolved)
        {
            try
            {
                var instance = pluginServiceProvider.GetRequiredService(pluginType);
                if (instance is not IPlugin plugin)
                {
                    _logger.LogWarning("Type '{Type}' does not implement IPlugin for plugin {PluginId}",
                        pluginType.FullName, entry.Id);
                    continue;
                }

                var mergedConfig = MergeConfigurationOverrides(entry.Id, entry.Config);
                await plugin.InitAsync(mergedConfig, ct);
                _plugins.Add(plugin);

                _logger.LogInformation("Loaded external plugin {PluginId} ({DisplayName}) v{Version}",
                    plugin.PluginId, plugin.DisplayName, plugin.Version);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize external plugin {PluginId}", entry.Id);
            }
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

    /// <summary>
    /// Resolve the assembly path and plugin type for an external plugin entry.
    /// Returns null if the entry cannot be resolved.
    /// </summary>
    private (Type PluginType, Assembly Assembly, string AssemblyPath)? ResolveExternalPlugin(PluginConfigEntry entry)
    {
        try
        {
            string assemblyPath;
            Type? pluginType;

            if (!string.IsNullOrEmpty(entry.Type))
            {
                // Parse "Namespace.Class, AssemblyReference" format
                var (typeName, resolvedPath) = ResolveType(entry.Type, _options.PluginsPath);
                assemblyPath = resolvedPath;

                var loadContext = new PluginLoadContext(assemblyPath);
                var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

                pluginType = assembly.GetType(typeName);
                if (pluginType == null)
                {
                    _logger.LogWarning(
                        "Type '{TypeName}' not found in {Assembly} for plugin {PluginId}",
                        typeName, Path.GetFileName(assemblyPath), entry.Id);
                    return null;
                }

                return (pluginType, assembly, assemblyPath);
            }

            // Fall back to assembly field + scan for IPlugin
            if (string.IsNullOrEmpty(entry.Assembly))
            {
                _logger.LogWarning("External plugin {PluginId} has no 'type' or 'assembly' specified", entry.Id);
                return null;
            }

            assemblyPath = Path.GetFullPath(Path.Combine(_options.PluginsPath, entry.Assembly));
            if (!File.Exists(assemblyPath))
            {
                _logger.LogWarning("Plugin assembly not found: {Path}", assemblyPath);
                return null;
            }

            var ctx = new PluginLoadContext(assemblyPath);
            var asm = ctx.LoadFromAssemblyPath(assemblyPath);
            pluginType = asm.GetTypes()
                .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            if (pluginType == null)
            {
                _logger.LogWarning("No IPlugin implementation found in {Path}", assemblyPath);
                return null;
            }

            return (pluginType, asm, assemblyPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve external plugin {PluginId}", entry.Id);
            return null;
        }
    }

    /// <summary>
    /// Parse a type specifier in "Namespace.Class, AssemblyReference" format and resolve the assembly path.
    /// The assembly reference can be an assembly name, relative path, or absolute path.
    /// </summary>
    private static (string TypeName, string AssemblyPath) ResolveType(string typeSpec, string baseDir)
    {
        var commaIndex = typeSpec.IndexOf(',');
        if (commaIndex < 0)
            throw new FormatException($"Invalid Type format: '{typeSpec}'. Expected 'Namespace.Class, AssemblyReference'.");

        var typeName = typeSpec[..commaIndex].Trim();
        var assemblyRef = typeSpec[(commaIndex + 1)..].Trim();

        // Resolve the assembly path:
        // - Contains path separator or ends with .dll → treat as a path
        // - Otherwise → assembly name, look for AssemblyName.dll in base dir
        string assemblyPath;
        if (assemblyRef.Contains(Path.DirectorySeparatorChar) ||
            assemblyRef.Contains(Path.AltDirectorySeparatorChar) ||
            assemblyRef.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            assemblyPath = Path.IsPathRooted(assemblyRef)
                ? assemblyRef
                : Path.GetFullPath(Path.Combine(baseDir, assemblyRef));
        }
        else
        {
            assemblyPath = Path.GetFullPath(Path.Combine(baseDir, assemblyRef + ".dll"));
        }

        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly not found: {assemblyPath}");

        return (typeName, assemblyPath);
    }

    /// <summary>
    /// AssemblyLoadContext with dependency resolution for plugin isolation.
    /// </summary>
    private class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path != null ? LoadFromAssemblyPath(path) : null;
        }
    }

}
