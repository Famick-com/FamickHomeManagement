using Famick.HomeManagement.Core.DTOs.Plugins;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Reads and writes the self-hosted <c>plugins/config.json</c> file, backing the
/// admin Plugins page. Changes don't apply in-process — operators restart the
/// app (the UI surfaces that as a banner).
/// </summary>
public interface IPluginConfigService
{
    /// <summary>
    /// Returns the union of (a) built-in plugins resolved from DI, (b) entries
    /// in <c>plugins/config.json</c>, and (c) DLL files in the plugins folder
    /// not yet referenced by any config entry. Secrets in each entry's config
    /// JSON are masked to <c>"***"</c>.
    /// </summary>
    Task<PluginConfigListDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates or replaces the entry with the given id. If <see cref="PluginConfigEntryDto.ConfigJson"/>
    /// contains <c>"***"</c> for any known secret field, the value is read back
    /// from the current on-disk file and substituted in — so refreshing the
    /// page and saving without changes doesn't blow secrets away.
    /// </summary>
    Task UpsertAsync(string id, PluginConfigEntryDto entry, CancellationToken ct = default);

    /// <summary>
    /// Removes the entry with the given id. Throws
    /// <see cref="InvalidOperationException"/> if the id is a built-in plugin
    /// (built-ins can be disabled, not removed).
    /// </summary>
    Task DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Registers a discovered DLL as a new (disabled) external plugin entry so
    /// the admin can then enable it and edit its config from the main list.
    /// </summary>
    Task RegisterDiscoveredAsync(
        string id,
        string assemblyPath,
        string typeFullName,
        string displayName,
        CancellationToken ct = default);
}
