using System.Text.Json.Serialization;

namespace Famick.HomeManagement.Core.Platform;

/// <summary>
/// The deployment platform the server is running on. Resolved once at startup
/// from <c>IsMultiTenantEnabled</c> + <c>HaIngress:Enabled</c> and consumed by
/// both the server and the SPA instead of re-deriving the flags ad hoc.
/// </summary>
/// <remarks>
/// Not to be confused with the mobile app's <c>ServerMode</c> enum, which
/// describes how the phone connects to a server — this is a server-side concept.
/// Serialized as its string name (not an int) so the JSON contract is readable
/// and stable across enum reordering.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ServerPlatform
{
    /// <summary>Single-tenant docker-compose / bare install.</summary>
    SelfHosted,

    /// <summary>Self-hosted image running as a Home Assistant add-on (Ingress enabled).</summary>
    HomeAssistant,

    /// <summary>Multi-tenant cloud SaaS.</summary>
    Cloud,
}
