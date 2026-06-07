using Microsoft.Extensions.Configuration;

namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Single source of truth for "where on disk does operator-mutable data live?".
/// All four derived paths (plugins, server-config overlay, ASP.NET Core Data
/// Protection keys, user uploads) default to subdirectories under
/// <c>Storage:Path</c> — so the docker-compose volume mount is a single
/// <c>./data:/app/data</c> and the dev <c>local_config/</c> folder mirrors it.
/// Every individual path is still overridable for fine-grained control.
/// </summary>
/// <remarks>
/// Relative paths resolve against <c>IHostEnvironment.ContentRootPath</c> so a
/// setting like <c>"data"</c> means the same thing whether the process was
/// launched from the project directory or the submodule root. Absolute paths
/// are used as-is.
/// </remarks>
public static class StoragePaths
{
    private const string DefaultStorageRoot = "data";

    public static string ResolveStorageRoot(IConfiguration configuration, string contentRootPath)
        => Resolve(configuration["Storage:Path"], contentRootPath, DefaultStorageRoot);

    public static string ResolvePluginsPath(IConfiguration configuration, string contentRootPath, string storageRoot)
        => Resolve(
            configuration["Plugins:Path"],
            contentRootPath,
            Path.Combine(storageRoot, "plugins"));

    public static string ResolveServerConfigPath(IConfiguration configuration, string contentRootPath, string storageRoot)
        => Resolve(
            configuration["ServerConfig:Path"],
            contentRootPath,
            Path.Combine(storageRoot, "config", "server-config.json"));

    public static string ResolveDataProtectionPath(IConfiguration configuration, string contentRootPath, string storageRoot)
        => Resolve(
            configuration["DataProtection:Path"],
            contentRootPath,
            Path.Combine(storageRoot, "dataprotection"));

    public static string ResolveUploadsPath(IConfiguration configuration, string contentRootPath, string storageRoot)
        => Resolve(
            configuration["Uploads:Path"],
            contentRootPath,
            Path.Combine(storageRoot, "uploads"));

    private static string Resolve(string? configured, string contentRootPath, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(configured) ? fallback : configured;
        return Path.IsPathRooted(value) ? value : Path.Combine(contentRootPath, value);
    }
}
