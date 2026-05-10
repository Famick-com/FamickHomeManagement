using System.Security.Claims;
using System.Text.Json;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Web.Shared.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Authorization;

public class StepUpFilterTests
{
    private const string Flag = "step_up_enabled";

    private static AuthorizationFilterContext BuildContext(
        long? authTime,
        IList<object> endpointMetadata,
        bool authenticated = true)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = "/api/v1/profile/change-password";
        if (authenticated)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            };
            if (authTime.HasValue)
            {
                claims.Add(new Claim("auth_time", authTime.Value.ToString(), ClaimValueTypes.Integer64));
            }
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
        }
        else
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        }

        var descriptor = new ActionDescriptor
        {
            EndpointMetadata = endpointMetadata
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor);
        return new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
    }

    private static StepUpFilter BuildFilter(
        bool flagEnabled = true,
        int configThreshold = 300)
    {
        var flags = new Mock<IFeatureFlagService>(MockBehavior.Strict);
        flags.Setup(f => f.IsEnabledAsync(Flag, It.IsAny<CancellationToken>()))
             .ReturnsAsync(flagEnabled);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:StepUpFreshnessSeconds"] = configThreshold.ToString()
            })
            .Build();

        return new StepUpFilter(flags.Object, config, NullLogger<StepUpFilter>.Instance);
    }

    private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    [Fact]
    public async Task NoAttribute_NoOp()
    {
        var filter = BuildFilter();
        var ctx = BuildContext(authTime: Now() - 9999, endpointMetadata: new List<object>());

        await filter.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task FlagOff_NoOpEvenWhenStale()
    {
        var filter = BuildFilter(flagEnabled: false);
        var ctx = BuildContext(authTime: Now() - 9999, endpointMetadata: new List<object> { new StepUpAttribute() });

        await filter.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task FreshAuthTime_NoOp()
    {
        var filter = BuildFilter(configThreshold: 300);
        var ctx = BuildContext(authTime: Now() - 60, endpointMetadata: new List<object> { new StepUpAttribute() });

        await filter.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task StaleAuthTime_Returns403StepUpRequired()
    {
        var filter = BuildFilter(configThreshold: 300);
        var ctx = BuildContext(authTime: Now() - 600, endpointMetadata: new List<object> { new StepUpAttribute() });

        await filter.OnAuthorizationAsync(ctx);

        var result = ctx.Result.Should().BeOfType<ContentResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        result.ContentType.Should().Be("application/json");
        var body = JsonSerializer.Deserialize<JsonElement>(result.Content!);
        body.GetProperty("code").GetString().Should().Be("STEP_UP_REQUIRED");
        body.GetProperty("error_message").GetString().Should().Be("Step-up authentication required");
    }

    [Fact]
    public async Task MissingAuthTimeClaim_FailsClosed()
    {
        var filter = BuildFilter();
        var ctx = BuildContext(authTime: null, endpointMetadata: new List<object> { new StepUpAttribute() });

        await filter.OnAuthorizationAsync(ctx);

        var result = ctx.Result.Should().BeOfType<ContentResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task PerEndpointOverride_BeatsConfigDefault()
    {
        // Config says 300s allowed; attribute says only 60s. auth_time 120s ago must
        // exceed the per-endpoint override and reject.
        var filter = BuildFilter(configThreshold: 300);
        var ctx = BuildContext(
            authTime: Now() - 120,
            endpointMetadata: new List<object> { new StepUpAttribute { FreshnessSeconds = 60 } });

        await filter.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeOfType<ContentResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task UnauthenticatedRequest_NoOp()
    {
        // [AllowAnonymous] + [StepUp] endpoints (e.g. passkey register/options) must
        // not be gated when there's no JWT — step-up is only meaningful for an
        // already-authenticated session.
        var filter = BuildFilter();
        var ctx = BuildContext(
            authTime: null,
            endpointMetadata: new List<object> { new StepUpAttribute() },
            authenticated: false);

        await filter.OnAuthorizationAsync(ctx);

        ctx.Result.Should().BeNull();
    }

    [Fact]
    public async Task BodyShape_ByteEqualAcrossPaths()
    {
        // Constant-shape regression: the rejection body must not leak which endpoint
        // failed. Two different stale-auth-time endpoints must produce the same body.
        var filter = BuildFilter();

        var ctxA = BuildContext(authTime: Now() - 600, endpointMetadata: new List<object> { new StepUpAttribute() });
        ctxA.HttpContext.Request.Path = "/api/v1/profile/change-password";

        var ctxB = BuildContext(authTime: Now() - 600, endpointMetadata: new List<object> { new StepUpAttribute() });
        ctxB.HttpContext.Request.Path = "/api/v1/recipes/abc/share";

        await filter.OnAuthorizationAsync(ctxA);
        await filter.OnAuthorizationAsync(ctxB);

        var bodyA = ((ContentResult)ctxA.Result!).Content;
        var bodyB = ((ContentResult)ctxB.Result!).Content;

        bodyA.Should().Be(bodyB);
    }
}
