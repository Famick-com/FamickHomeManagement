namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Configuration for the Home Assistant Ingress authentication scheme.
/// </summary>
/// <remarks>
/// HA Supervisor authenticates the HA user at the edge and forwards the request
/// to the add-on with X-Remote-User-* headers. The add-on only receives ingress
/// requests over Supervisor's internal Docker network, so the headers can be
/// trusted when the source IP is in <see cref="TrustedProxies"/>.
/// </remarks>
public class HaIngressSettings
{
    public const string SectionName = "HaIngress";

    /// <summary>
    /// Master switch. When false (the default) the HA Ingress auth scheme
    /// short-circuits with no result and the request falls through to the
    /// regular JWT bearer scheme. Set to true only when running as an HA add-on.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// CIDR ranges of source IPs allowed to assert HA-Ingress identity headers.
    /// Empty list means "no source is trusted" — defense against a misconfigured
    /// reverse proxy forwarding spoofed headers from the public internet.
    /// Typical value for Supervisor ingress is the 172.30.32.0/23 docker network.
    /// </summary>
    public List<string> TrustedProxies { get; set; } = new();
}
