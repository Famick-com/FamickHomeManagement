using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Server;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class ServerConfigService : IServerConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _configPath;
    private readonly ILogger<ServerConfigService> _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public ServerConfigService(string configPath, ILogger<ServerConfigService> logger)
    {
        _configPath = configPath;
        _logger = logger;
    }

    public async Task<ServerConfigDto> GetAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_configPath))
        {
            return new ServerConfigDto();
        }

        await using var stream = File.OpenRead(_configPath);
        var dto = await JsonSerializer.DeserializeAsync<ServerConfigDto>(stream, JsonOptions, ct);
        return dto ?? new ServerConfigDto();
    }

    public async Task UpdateAsync(ServerConfigDto config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _writeLock.WaitAsync(ct);
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to a sibling temp file then atomically swap into place. This
            // keeps the file-watcher (reloadOnChange) from ever observing a
            // partially-written JSON document.
            var tempPath = _configPath + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, config, JsonOptions, ct);
                await stream.FlushAsync(ct);
            }
            File.Move(tempPath, _configPath, overwrite: true);
            _logger.LogInformation("server-config.json updated at {Path}", _configPath);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SetSetupCompleteAsync(bool isComplete, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        current.Server.SetupComplete = isComplete;
        await UpdateAsync(current, ct);
    }
}
