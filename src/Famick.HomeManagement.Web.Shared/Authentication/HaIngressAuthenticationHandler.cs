using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Web.Shared.Authentication;

/// <summary>
/// Trusts the <c>X-Remote-User-*</c> headers set by HA Supervisor when the
/// request originates from a configured trusted source (Supervisor's docker
/// network). Resolves the HA user GUID to a local Famick user via
/// <see cref="IHaIngressUserResolver"/> and issues a <see cref="ClaimsPrincipal"/>
/// whose <c>sub</c> claim matches the local user id — so downstream code that
/// resolves the caller via <c>NameClaimType = "sub"</c> works identically to
/// the JWT bearer path.
/// </summary>
public class HaIngressAuthenticationHandler : AuthenticationHandler<HaIngressAuthenticationOptions>
{
    private readonly IOptionsMonitor<HaIngressSettings> _settings;
    private readonly IHaIngressUserResolver _userResolver;

    public HaIngressAuthenticationHandler(
        IOptionsMonitor<HaIngressAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptionsMonitor<HaIngressSettings> settings,
        IHaIngressUserResolver userResolver)
        : base(options, logger, encoder)
    {
        _settings = settings;
        _userResolver = userResolver;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settings = _settings.CurrentValue;
        if (!settings.Enabled)
        {
            return AuthenticateResult.NoResult();
        }

        var haUserId = Request.Headers[HaIngressAuthenticationDefaults.UserIdHeader].ToString();
        if (string.IsNullOrWhiteSpace(haUserId))
        {
            return AuthenticateResult.NoResult();
        }

        var remoteIp = Context.Connection.RemoteIpAddress;
        if (remoteIp is null || !IsTrustedSource(remoteIp, settings.TrustedProxies))
        {
            Logger.LogWarning(
                "Rejecting HA Ingress identity header from untrusted source {RemoteIp}",
                remoteIp);
            return AuthenticateResult.Fail("HA Ingress headers not accepted from this source");
        }

        var identity = new HaIngressIdentity(
            HaUserId: haUserId,
            Username: NullIfBlank(Request.Headers[HaIngressAuthenticationDefaults.UserNameHeader].ToString()),
            DisplayName: NullIfBlank(Request.Headers[HaIngressAuthenticationDefaults.UserDisplayNameHeader].ToString()));

        try
        {
            var user = await _userResolver.ResolveAsync(identity, Context.RequestAborted);
            var principal = BuildPrincipal(user.Id, identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resolve HA Ingress user {HaUserId}", haUserId);
            return AuthenticateResult.Fail(ex);
        }
    }

    private ClaimsPrincipal BuildPrincipal(Guid userId, HaIngressIdentity identity)
    {
        var claims = new List<Claim>
        {
            // Match the JwtBearer scheme's NameClaimType = "sub" so downstream
            // code reads the same identifier regardless of which scheme fired.
            new("sub", userId.ToString()),
        };
        if (!string.IsNullOrWhiteSpace(identity.DisplayName))
        {
            claims.Add(new Claim("name", identity.DisplayName));
        }
        var identityCookie = new ClaimsIdentity(claims, Scheme.Name, nameType: "sub", roleType: "role");
        return new ClaimsPrincipal(identityCookie);
    }

    private static bool IsTrustedSource(IPAddress remote, IList<string> cidrs)
    {
        if (cidrs.Count == 0)
        {
            return false;
        }
        var normalized = remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote;
        foreach (var raw in cidrs)
        {
            if (IPNetwork.TryParse(raw, out var net) && net.Contains(normalized))
            {
                return true;
            }
        }
        return false;
    }

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
