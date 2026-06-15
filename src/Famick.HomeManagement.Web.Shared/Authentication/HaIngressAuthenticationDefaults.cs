namespace Famick.HomeManagement.Web.Shared.Authentication;

/// <summary>
/// Constants for the Home Assistant Ingress authentication scheme.
/// </summary>
public static class HaIngressAuthenticationDefaults
{
    /// <summary>
    /// Authentication scheme name registered with ASP.NET Core auth.
    /// </summary>
    public const string AuthenticationScheme = "HaIngress";

    /// <summary>
    /// Policy scheme that picks between HaIngress and the regular JWT bearer scheme
    /// per request, based on whether the HA Ingress identity header is present.
    /// Used as <see cref="Microsoft.AspNetCore.Authentication.AuthenticationOptions.DefaultAuthenticateScheme"/>
    /// when HA Ingress is enabled.
    /// </summary>
    public const string MultiplexPolicyScheme = "HaIngressOrJwt";

    /// <summary>
    /// HA Supervisor sets this header to the authenticated HA user's GUID.
    /// </summary>
    public const string UserIdHeader = "X-Remote-User-Id";

    /// <summary>
    /// HA Supervisor sets this header to the authenticated HA user's username.
    /// </summary>
    public const string UserNameHeader = "X-Remote-User-Name";

    /// <summary>
    /// HA Supervisor sets this header to the authenticated HA user's display name.
    /// </summary>
    public const string UserDisplayNameHeader = "X-Remote-User-Display-Name";
}
