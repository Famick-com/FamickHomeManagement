namespace Famick.HomeManagement.Core.DTOs.Plugins;

/// <summary>
/// Identifies where the entry came from. Drives the badge in the admin UI.
/// </summary>
public enum PluginSource
{
    /// <summary>Resolved from DI; always present even when not in config.json.</summary>
    Builtin,

    /// <summary>Configured via plugins/config.json (built-in or external).</summary>
    Configured,

    /// <summary>DLL file present in plugins/ but not referenced by any config entry.</summary>
    Discovered,
}

/// <summary>
/// Top-level shape for the admin Plugins page: every plugin the operator can
/// act on plus any discovered DLL files awaiting registration.
/// </summary>
public class PluginConfigListDto
{
    public List<PluginConfigEntryDto> Plugins { get; set; } = new();
    public List<DiscoveredDllDto> Discovered { get; set; } = new();
}

/// <summary>
/// A single row in the admin Plugins table. Round-trips through the GET / PUT
/// endpoints. <see cref="ConfigJson"/> holds the plugin's <c>config</c> object
/// as pretty-printed JSON; secrets in that JSON are masked to <c>"***"</c> on
/// the way out and preserved (re-read from disk) on the way back in.
/// </summary>
public class PluginConfigEntryDto
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool Builtin { get; set; }
    public string? Type { get; set; }
    public string? Assembly { get; set; }
    public string? ConfigJson { get; set; }
    public PluginSource Source { get; set; }
}

/// <summary>
/// A DLL file the operator can register as a new plugin entry.
/// </summary>
public class DiscoveredDllDto
{
    public string FileName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
}
