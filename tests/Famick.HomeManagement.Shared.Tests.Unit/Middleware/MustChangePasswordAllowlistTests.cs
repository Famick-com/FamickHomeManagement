using System.Security.Claims;
using Famick.HomeManagement.Web.Shared.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Shared.Tests.Unit.Middleware;

public class MustChangePasswordAllowlistTests
{
    private static async Task<(int statusCode, bool nextCalled)> RunAsync(string path, bool mustChange = true)
    {
        var nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new MustChangePasswordMiddleware(next);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        var claims = new List<Claim>();
        if (mustChange)
        {
            claims.Add(new Claim("must_change_password", "true"));
        }
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        await middleware.InvokeAsync(ctx);

        return (ctx.Response.StatusCode, nextCalled);
    }

    [Theory]
    [InlineData("/api/v1/profile/change-password")]
    [InlineData("/api/auth/accept-terms")]
    [InlineData("/api/auth/logout")]
    [InlineData("/api/auth/logout-all")]
    [InlineData("/api/v1/profile")]
    public async Task PreExistingAllowlist_StillBypasses(string path)
    {
        var (status, nextCalled) = await RunAsync(path);
        status.Should().Be(200);
        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/auth/passkey/authenticate/options")]
    [InlineData("/api/auth/passkey/authenticate/verify")]
    [InlineData("/api/auth/reauth")]
    public async Task Phase2NewExactAllowlist_Bypasses(string path)
    {
        var (status, nextCalled) = await RunAsync(path);
        status.Should().Be(200);
        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/auth/external/google/challenge")]
    [InlineData("/api/auth/external/apple/challenge")]
    [InlineData("/api/auth/external/google/callback")]
    [InlineData("/api/auth/external/apple/callback")]
    [InlineData("/api/auth/external/google/native")]
    [InlineData("/api/auth/external/apple/native")]
    public async Task Phase2NewExternalAuthFlow_Bypasses(string path)
    {
        var (status, nextCalled) = await RunAsync(path);
        status.Should().Be(200);
        nextCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("/api/auth/passkey/register/options")]
    [InlineData("/api/auth/passkey/register/verify")]
    [InlineData("/api/auth/external/google/link")]
    [InlineData("/api/auth/external/google/link/verify")]
    [InlineData("/api/v1/contacts")]
    [InlineData("/api/v1/recipes")]
    public async Task NonAllowlistedPaths_Blocked(string path)
    {
        var (status, nextCalled) = await RunAsync(path);
        status.Should().Be(StatusCodes.Status403Forbidden);
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task MustChangeFalse_AnythingPasses()
    {
        var (status, nextCalled) = await RunAsync("/api/v1/contacts", mustChange: false);
        status.Should().Be(200);
        nextCalled.Should().BeTrue();
    }
}
