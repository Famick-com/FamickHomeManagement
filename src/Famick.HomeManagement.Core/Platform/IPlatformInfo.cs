namespace Famick.HomeManagement.Core.Platform;

/// <summary>
/// First-class, startup-resolved view of which <see cref="ServerPlatform"/> the
/// server is running on. Registered as a singleton; inject this instead of
/// re-checking <c>IsMultiTenantEnabled</c> / <c>HaIngress:Enabled</c> for
/// platform-presentation decisions.
/// </summary>
public interface IPlatformInfo
{
    /// <summary>The resolved deployment platform.</summary>
    ServerPlatform Platform { get; }

    /// <summary><c>true</c> when running as a single-tenant self-hosted install.</summary>
    bool IsSelfHosted { get; }

    /// <summary><c>true</c> when running as a Home Assistant add-on.</summary>
    bool IsHomeAssistant { get; }

    /// <summary><c>true</c> when running as the multi-tenant cloud SaaS.</summary>
    bool IsCloud { get; }
}
