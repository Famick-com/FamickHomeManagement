using Microsoft.AspNetCore.Authentication;

namespace Famick.HomeManagement.Web.Shared.Authentication;

/// <summary>
/// Authentication-scheme options for HA Ingress. Per-scheme config (enabled
/// flag + trusted-proxy CIDRs) lives in <see cref="Famick.HomeManagement.Core.Configuration.HaIngressSettings"/>
/// and is read at handler-invocation time so reload-on-change works.
/// </summary>
public class HaIngressAuthenticationOptions : AuthenticationSchemeOptions
{
}
