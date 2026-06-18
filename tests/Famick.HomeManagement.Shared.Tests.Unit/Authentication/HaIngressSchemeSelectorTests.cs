using Famick.HomeManagement.Web.Shared.Authentication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Shared.Tests.Unit.Authentication;

/// <summary>
/// The multiplex scheme selector must prefer the app's JWT (which carries
/// roles/permissions) whenever the client presents a bearer token. If it always
/// routed Ingress requests to the header scheme, RequireAdmin endpoints would
/// 403 even for an admin because the header principal has no role claim.
/// </summary>
public class HaIngressSchemeSelectorTests
{
    private const string Jwt = "Bearer";
    private const string Ingress = HaIngressAuthenticationDefaults.AuthenticationScheme;

    private static HttpContext Context(string? authorization, bool ingressHeader)
    {
        var ctx = new DefaultHttpContext();
        if (authorization is not null)
        {
            ctx.Request.Headers.Authorization = authorization;
        }
        if (ingressHeader)
        {
            ctx.Request.Headers[HaIngressAuthenticationDefaults.UserIdHeader] = "ha-user-1";
        }
        return ctx;
    }

    [Fact]
    public void BearerPresent_PrefersJwt_EvenWithIngressHeader()
    {
        // The authenticated SPA: carries the SSO-minted JWT AND (always, under
        // Ingress) the Supervisor identity header. Must authenticate via JWT so
        // roles are present.
        HaIngressAuthenticationExtensions.SelectScheme(
            Context("Bearer abc.def.ghi", ingressHeader: true), Jwt)
            .Should().Be(Jwt);
    }

    [Fact]
    public void NoBearer_WithIngressHeader_UsesIngressScheme()
    {
        // The token-less SSO handshake.
        HaIngressAuthenticationExtensions.SelectScheme(
            Context(authorization: null, ingressHeader: true), Jwt)
            .Should().Be(Ingress);
    }

    [Fact]
    public void NoBearer_NoIngressHeader_FallsBackToJwt()
    {
        HaIngressAuthenticationExtensions.SelectScheme(
            Context(authorization: null, ingressHeader: false), Jwt)
            .Should().Be(Jwt);
    }

    [Fact]
    public void BearerPresent_NoIngressHeader_UsesJwt()
    {
        HaIngressAuthenticationExtensions.SelectScheme(
            Context("Bearer abc", ingressHeader: false), Jwt)
            .Should().Be(Jwt);
    }

    [Fact]
    public void NonBearerAuthorization_WithIngressHeader_UsesIngressScheme()
    {
        // A non-bearer Authorization value isn't the app's JWT, so the trusted
        // header identity still wins.
        HaIngressAuthenticationExtensions.SelectScheme(
            Context("Basic dXNlcjpwYXNz", ingressHeader: true), Jwt)
            .Should().Be(Ingress);
    }
}
