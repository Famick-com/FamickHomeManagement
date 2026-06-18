using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Web.Shared.Authentication;

/// <summary>
/// Registers the HA Ingress authentication scheme and the policy scheme that
/// multiplexes it with JWT bearer based on whether the
/// <c>X-Remote-User-Id</c> header is present.
/// </summary>
public static class HaIngressAuthenticationExtensions
{
    /// <summary>
    /// Binds <see cref="HaIngressSettings"/> and registers the resolver.
    /// Call this on the service collection regardless of whether the scheme
    /// is currently enabled — the handler short-circuits at runtime when
    /// <see cref="HaIngressSettings.Enabled"/> is false.
    /// </summary>
    public static IServiceCollection AddHaIngressUserResolution(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<HaIngressSettings>(configuration.GetSection(HaIngressSettings.SectionName));
        services.AddScoped<IHaIngressUserResolver, HaIngressUserResolver>();
        // Issues a normal JWT session for the resolved Ingress user, backed by
        // AuthenticationService (same token issuance as password login). Scoped
        // and stateless, so a dedicated instance here is harmless.
        services.AddScoped<IHaIngressSessionIssuer, Infrastructure.Services.AuthenticationService>();
        return services;
    }

    /// <summary>
    /// Adds the HA Ingress scheme to the auth builder. Must be called as
    /// part of the existing <c>AddAuthentication(...)</c> chain alongside
    /// <c>AddJwtBearer(...)</c>.
    /// </summary>
    public static AuthenticationBuilder AddHaIngress(this AuthenticationBuilder builder)
        => builder.AddScheme<HaIngressAuthenticationOptions, HaIngressAuthenticationHandler>(
            HaIngressAuthenticationDefaults.AuthenticationScheme,
            displayName: null,
            configureOptions: _ => { });

    /// <summary>
    /// Adds a policy scheme that forwards each request to either the HA Ingress
    /// scheme or the supplied fallback scheme based on header presence. Use this
    /// scheme name as <see cref="AuthenticationOptions.DefaultAuthenticateScheme"/>.
    /// </summary>
    /// <param name="builder">Auth builder.</param>
    /// <param name="fallbackScheme">Scheme to forward to when the HA Ingress header is absent (typically JWT bearer).</param>
    public static AuthenticationBuilder AddHaIngressOrFallbackPolicyScheme(
        this AuthenticationBuilder builder,
        string fallbackScheme)
        => builder.AddPolicyScheme(
            HaIngressAuthenticationDefaults.MultiplexPolicyScheme,
            displayName: HaIngressAuthenticationDefaults.MultiplexPolicyScheme,
            configureOptions: options =>
            {
                options.ForwardDefaultSelector = context => SelectScheme(context, fallbackScheme);
            });

    /// <summary>
    /// Chooses which scheme authenticates a request behind the multiplex.
    /// Prefers the app's own JWT whenever the client presents a bearer token —
    /// the JWT carries the user's roles/permissions, which the bare HA Ingress
    /// identity headers (sub + name only) do not. Routing every Ingress request
    /// to the header scheme would make role-gated endpoints (e.g. RequireAdmin)
    /// return 403 even for an admin. The HA Ingress scheme is used only for the
    /// token-less SSO handshake (and any other request with no bearer token but
    /// a trusted identity header).
    /// </summary>
    public static string SelectScheme(HttpContext context, string fallbackScheme)
    {
        var hasBearer = context.Request.Headers.Authorization
            .ToString()
            .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

        if (!hasBearer && context.Request.Headers.ContainsKey(HaIngressAuthenticationDefaults.UserIdHeader))
        {
            return HaIngressAuthenticationDefaults.AuthenticationScheme;
        }

        return fallbackScheme;
    }
}
