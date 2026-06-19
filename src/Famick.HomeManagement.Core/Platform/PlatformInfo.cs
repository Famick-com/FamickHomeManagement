namespace Famick.HomeManagement.Core.Platform;

/// <summary>
/// Immutable <see cref="IPlatformInfo"/> implementation. Constructed once at
/// startup with the resolved <see cref="ServerPlatform"/>; the convenience bools
/// are derived from it.
/// </summary>
public sealed class PlatformInfo : IPlatformInfo
{
    public PlatformInfo(ServerPlatform platform)
    {
        Platform = platform;
    }

    public ServerPlatform Platform { get; }

    public bool IsSelfHosted => Platform == ServerPlatform.SelfHosted;

    public bool IsHomeAssistant => Platform == ServerPlatform.HomeAssistant;

    public bool IsCloud => Platform == ServerPlatform.Cloud;
}
