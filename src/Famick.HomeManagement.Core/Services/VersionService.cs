using System.Reflection;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// Provides access to application version information.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the semantic version (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the informational version including git commit hash (e.g., "1.0.0+abc1234").
    /// </summary>
    string InformationalVersion { get; }
}

/// <summary>
/// Default implementation that reads version from assembly metadata set by MinVer.
/// </summary>
public class VersionService : IVersionService
{
    public string Version { get; }
    public string InformationalVersion { get; }

    public VersionService()
    {
        var assembly = typeof(VersionService).Assembly;

        // Get the informational version (includes git commit hash)
        InformationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "0.0.0";

        // Extract just the version part (before the + if present)
        var plusIndex = InformationalVersion.IndexOf('+');
        Version = plusIndex > 0
            ? InformationalVersion[..plusIndex]
            : InformationalVersion;
    }
}
