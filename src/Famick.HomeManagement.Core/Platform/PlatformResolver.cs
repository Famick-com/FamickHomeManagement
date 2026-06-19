namespace Famick.HomeManagement.Core.Platform;

/// <summary>
/// Pure resolution of <see cref="ServerPlatform"/> from the two existing config
/// flags. Kept dependency-free (takes plain bools) so the truth table is trivial
/// to unit test.
/// </summary>
public static class PlatformResolver
{
    /// <summary>
    /// Resolves the platform. Multi-tenant always wins over Ingress: a cloud
    /// deployment never runs as an HA add-on, whereas an HA add-on runs the same
    /// single-tenant image as self-hosted plus the Ingress flag.
    /// </summary>
    public static ServerPlatform Resolve(bool isMultiTenantEnabled, bool haIngressEnabled) =>
        isMultiTenantEnabled ? ServerPlatform.Cloud
        : haIngressEnabled ? ServerPlatform.HomeAssistant
        : ServerPlatform.SelfHosted;
}
