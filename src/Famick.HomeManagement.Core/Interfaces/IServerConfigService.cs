using Famick.HomeManagement.Core.DTOs.Server;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Reads and writes the self-hosted <c>config/server-config.json</c> overlay
/// shared by the first-run wizard, the admin "Server Settings" page, and direct
/// edits on the host. The file is reloaded by ASP.NET's configuration system on
/// change, so callers don't need to broadcast updates — <c>IOptionsMonitor</c>
/// subscribers re-bind automatically.
/// </summary>
public interface IServerConfigService
{
    /// <summary>
    /// Returns the parsed file contents, or a default DTO if the file is missing.
    /// </summary>
    Task<ServerConfigDto> GetAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomically replaces the file. Callers that only mean to update a subset of
    /// fields should call <see cref="GetAsync"/> first, mutate the result, then
    /// pass it here — the write is a full replace.
    /// </summary>
    Task UpdateAsync(ServerConfigDto config, CancellationToken ct = default);

    /// <summary>
    /// Convenience for the final wizard step: flips
    /// <see cref="ServerSection.SetupComplete"/> while preserving every other
    /// field in the current file.
    /// </summary>
    Task SetSetupCompleteAsync(bool isComplete, CancellationToken ct = default);
}
