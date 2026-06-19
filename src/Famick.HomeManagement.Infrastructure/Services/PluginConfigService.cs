using System.Text.Json;
using System.Text.Json.Nodes;
using Famick.HomeManagement.Core.DTOs.Plugins;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Plugin.Abstractions;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class PluginConfigService : IPluginConfigService
{
    private const string MaskedValue = "***";

    /// <summary>
    /// Case-insensitive set of config property names treated as secrets. Values
    /// under any of these keys are masked in GET responses and preserved across
    /// round-trips if the client sends back <c>"***"</c>.
    /// </summary>
    private static readonly HashSet<string> SecretFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "clientSecret",
        "apiKey",
        "password",
        "token",
        "accessToken",
        "refreshToken",
        "secret",
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly string _pluginsPath;
    private readonly string _configPath;
    private readonly IReadOnlyDictionary<string, IPlugin> _builtinPlugins;
    private readonly Core.Interfaces.Plugins.IPluginLoader _pluginLoader;
    private readonly ILogger<PluginConfigService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public PluginConfigService(
        string pluginsPath,
        IEnumerable<IPlugin> builtinPlugins,
        Core.Interfaces.Plugins.IPluginLoader pluginLoader,
        ILogger<PluginConfigService> logger)
    {
        _pluginsPath = pluginsPath;
        _configPath = Path.Combine(pluginsPath, "config.json");
        _builtinPlugins = builtinPlugins.ToDictionary(p => p.PluginId, p => p);
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    /// <summary>
    /// Resolves a plugin's documentation/help URL from its loaded instance
    /// (built-in or externally loaded). The config entry's <c>helpUrl</c> field,
    /// when present, overrides this.
    /// </summary>
    private string? ResolveHelpUrl(string id)
    {
        if (_builtinPlugins.TryGetValue(id, out var builtin) && !string.IsNullOrWhiteSpace(builtin.HelpUrl))
            return builtin.HelpUrl;

        return _pluginLoader.Plugins.FirstOrDefault(p => p.PluginId == id)?.HelpUrl;
    }

    public async Task<PluginConfigListDto> GetAsync(CancellationToken ct = default)
    {
        var fileEntries = await ReadEntriesAsync(ct);
        var entriesById = fileEntries.ToDictionary(e => GetId(e), StringComparer.Ordinal);

        var result = new PluginConfigListDto();

        // Built-ins first: always shown. Use the file entry if one exists so
        // operator-set Enabled / Config survives; synthesize defaults otherwise.
        foreach (var (id, plugin) in _builtinPlugins.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            if (entriesById.TryGetValue(id, out var existing))
            {
                result.Plugins.Add(ToDto(existing, PluginSource.Builtin, redact: true));
                entriesById.Remove(id);
            }
            else
            {
                result.Plugins.Add(new PluginConfigEntryDto
                {
                    Id = id,
                    DisplayName = plugin.DisplayName,
                    Enabled = true,
                    Builtin = true,
                    Source = PluginSource.Builtin,
                    HelpUrl = plugin.HelpUrl,
                });
            }
        }

        // External / configured entries the user added that aren't built-ins.
        foreach (var entry in entriesById.Values)
        {
            result.Plugins.Add(ToDto(entry, PluginSource.Configured, redact: true));
        }

        // Discovered DLLs in the plugins folder that aren't already referenced.
        result.Discovered = ScanDiscoveredDlls(fileEntries);

        return result;
    }

    public async Task UpsertAsync(string id, PluginConfigEntryDto entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(entry);

        await _writeLock.WaitAsync(ct);
        try
        {
            var fileEntries = await ReadEntriesAsync(ct);
            var existingIndex = fileEntries.FindIndex(e => string.Equals(GetId(e), id, StringComparison.Ordinal));
            var existing = existingIndex >= 0 ? fileEntries[existingIndex] : null;

            var newEntry = BuildFileEntry(id, entry, existingConfig: existing?["config"] as JsonObject);

            if (existingIndex >= 0)
            {
                fileEntries[existingIndex] = newEntry;
            }
            else
            {
                fileEntries.Add(newEntry);
            }

            await WriteEntriesAsync(fileEntries, ct);
            _logger.LogInformation("plugins/config.json upserted entry {Id}", id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_builtinPlugins.ContainsKey(id))
        {
            throw new InvalidOperationException(
                $"Plugin '{id}' is built-in and cannot be removed. Disable it instead.");
        }

        await _writeLock.WaitAsync(ct);
        try
        {
            var fileEntries = await ReadEntriesAsync(ct);
            var removed = fileEntries.RemoveAll(e => string.Equals(GetId(e), id, StringComparison.Ordinal));
            if (removed == 0)
            {
                return;
            }
            await WriteEntriesAsync(fileEntries, ct);
            _logger.LogInformation("plugins/config.json removed entry {Id}", id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task RegisterDiscoveredAsync(
        string id,
        string assemblyPath,
        string typeFullName,
        string displayName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeFullName);

        var stub = new PluginConfigEntryDto
        {
            Id = id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName,
            Enabled = false,
            Builtin = false,
            Type = typeFullName,
            Assembly = assemblyPath,
            ConfigJson = null,
        };
        await UpsertAsync(id, stub, ct);
    }

    private async Task<List<JsonObject>> ReadEntriesAsync(CancellationToken ct)
    {
        if (!File.Exists(_configPath))
        {
            return new List<JsonObject>();
        }

        await using var stream = File.OpenRead(_configPath);
        var node = await JsonNode.ParseAsync(stream, cancellationToken: ct);
        var pluginsArray = node?["plugins"] as JsonArray;
        if (pluginsArray is null)
        {
            return new List<JsonObject>();
        }

        var result = new List<JsonObject>();
        foreach (var item in pluginsArray)
        {
            if (item is JsonObject obj)
            {
                // Detach from the parent array so we can mutate freely.
                result.Add(JsonNode.Parse(obj.ToJsonString())!.AsObject());
            }
        }
        return result;
    }

    private async Task WriteEntriesAsync(List<JsonObject> entries, CancellationToken ct)
    {
        Directory.CreateDirectory(_pluginsPath);

        var root = new JsonObject
        {
            ["plugins"] = new JsonArray(entries.Select(e => (JsonNode?)JsonNode.Parse(e.ToJsonString())).ToArray()),
        };

        var tempPath = _configPath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, root, JsonOptions, ct);
            await stream.FlushAsync(ct);
        }
        File.Move(tempPath, _configPath, overwrite: true);
    }

    private List<DiscoveredDllDto> ScanDiscoveredDlls(IReadOnlyList<JsonObject> fileEntries)
    {
        if (!Directory.Exists(_pluginsPath))
        {
            return new List<DiscoveredDllDto>();
        }

        var referenced = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in fileEntries)
        {
            var asm = entry["assembly"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(asm))
            {
                referenced.Add(asm);
                referenced.Add(Path.GetFileName(asm));
            }
        }

        var result = new List<DiscoveredDllDto>();
        foreach (var fullPath in Directory.EnumerateFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(fullPath);
            var relativePath = Path.GetRelativePath(_pluginsPath, fullPath);
            if (referenced.Contains(fileName) || referenced.Contains(relativePath))
            {
                continue;
            }
            result.Add(new DiscoveredDllDto { FileName = fileName, RelativePath = relativePath });
        }
        return result;
    }

    private static string GetId(JsonObject obj) => obj["id"]?.GetValue<string>() ?? string.Empty;

    private PluginConfigEntryDto ToDto(JsonObject entry, PluginSource source, bool redact)
    {
        var id = GetId(entry);
        var dto = new PluginConfigEntryDto
        {
            Id = id,
            DisplayName = entry["displayName"]?.GetValue<string>() ?? string.Empty,
            Enabled = entry["enabled"]?.GetValue<bool>() ?? false,
            Builtin = entry["builtin"]?.GetValue<bool>() ?? false,
            Type = entry["type"]?.GetValue<string>(),
            Assembly = entry["assembly"]?.GetValue<string>(),
            HelpUrl = entry["helpUrl"]?.GetValue<string>()
                ?? ResolveHelpUrl(id),
            Source = source,
        };

        if (entry["config"] is JsonObject configObj)
        {
            var prepared = redact ? RedactSecrets(configObj) : configObj;
            dto.ConfigJson = prepared.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        return dto;
    }

    private static JsonObject RedactSecrets(JsonObject source)
    {
        var clone = JsonNode.Parse(source.ToJsonString())!.AsObject();
        Walk(clone);
        return clone;

        static void Walk(JsonObject obj)
        {
            foreach (var key in obj.Select(kv => kv.Key).ToList())
            {
                var value = obj[key];
                if (SecretFieldNames.Contains(key) && value is JsonValue)
                {
                    obj[key] = MaskedValue;
                }
                else if (value is JsonObject nested)
                {
                    Walk(nested);
                }
            }
        }
    }

    private static JsonObject BuildFileEntry(string id, PluginConfigEntryDto dto, JsonObject? existingConfig)
    {
        var entry = new JsonObject
        {
            ["id"] = id,
            ["enabled"] = dto.Enabled,
            ["builtin"] = dto.Builtin,
            ["displayName"] = dto.DisplayName,
        };
        if (!string.IsNullOrWhiteSpace(dto.Type)) entry["type"] = dto.Type;
        if (!string.IsNullOrWhiteSpace(dto.Assembly)) entry["assembly"] = dto.Assembly;
        if (!string.IsNullOrWhiteSpace(dto.HelpUrl)) entry["helpUrl"] = dto.HelpUrl;

        if (!string.IsNullOrWhiteSpace(dto.ConfigJson))
        {
            var parsed = JsonNode.Parse(dto.ConfigJson) as JsonObject
                ?? throw new InvalidOperationException("Plugin config JSON must be a JSON object.");
            entry["config"] = PreserveMaskedSecrets(parsed, existingConfig);
        }

        return entry;
    }

    /// <summary>
    /// If a field with a known secret name was sent back as <c>"***"</c>, look
    /// up the value from <paramref name="existingConfig"/> and substitute it
    /// in. Lets the round-trip "fetch redacted → save unchanged" preserve secrets.
    /// </summary>
    private static JsonObject PreserveMaskedSecrets(JsonObject incoming, JsonObject? existingConfig)
    {
        if (existingConfig is null)
        {
            return incoming;
        }
        Walk(incoming, existingConfig);
        return incoming;

        static void Walk(JsonObject inc, JsonObject existing)
        {
            foreach (var key in inc.Select(kv => kv.Key).ToList())
            {
                var incValue = inc[key];
                var existingValue = existing[key];

                if (SecretFieldNames.Contains(key)
                    && incValue is JsonValue v
                    && v.TryGetValue<string>(out var s)
                    && s == MaskedValue
                    && existingValue is not null)
                {
                    inc[key] = JsonNode.Parse(existingValue.ToJsonString());
                }
                else if (incValue is JsonObject nestedInc && existingValue is JsonObject nestedExisting)
                {
                    Walk(nestedInc, nestedExisting);
                }
            }
        }
    }
}
