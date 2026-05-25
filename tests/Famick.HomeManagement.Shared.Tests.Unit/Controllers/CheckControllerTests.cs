using System.Diagnostics;
using System.Text.Json;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Shared.Captcha;
using Famick.HomeManagement.Shared.RateLimit;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FlagNames = Famick.HomeManagement.FeatureFlags.FeatureFlags;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Phase 4 chunk 4.C — <see cref="CheckController"/>. Exercises feature flag
/// gating, rate-limit branching, CAPTCHA gate, and constant-shape success
/// path. The constant-time envelope itself is asserted statistically by the
/// 4.I CI test (1000-shot harness); these unit tests only verify the lower
/// bound is honored.
///
/// Phase 5 chunk 5.B — moved from Cloud.Tests.Unit alongside the controller's
/// promotion to Web.Shared. No behavioral change; namespace + using updates only.
/// </summary>
public class CheckControllerTests
{
    private readonly Mock<IFeatureFlagService> _featureFlags = new();
    private readonly Mock<ICheckRateLimiter> _rateLimiter = new();
    private readonly Mock<ICaptchaService> _captcha = new();

    private CheckController BuildSut(HomeManagementDbContext db)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RecaptchaSettings:SiteKey"] = "test-site-key",
            })
            .Build();

        var sut = new CheckController(
            db,
            _featureFlags.Object,
            _rateLimiter.Object,
            _captcha.Object,
            config,
            NullLogger<CheckController>.Instance);

        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        sut.ControllerContext.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.0.2.1");

        return sut;
    }

    private static HomeManagementDbContext BuildDb()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"check-{Guid.NewGuid():N}")
            .Options;
        return new HomeManagementDbContext(options);
    }

    private void SetRateLimitAllowed(long ipHourCount = 1)
    {
        _rateLimiter
            .Setup(r => r.ObserveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckRateLimitDecision(
                CheckRateLimitOutcome.Allowed,
                IpHourCount: ipHourCount,
                IpMinuteCount: 1,
                EmailHourCount: 1,
                RetryAfter: null));
    }

    [Fact]
    public async Task Check_returns_404_when_flag_disabled()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(false);

        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.Check(new CheckRequest { Email = "anyone@example.com" }, default);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Check_returns_cloud_for_unknown_email_when_allowed()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        SetRateLimitAllowed();

        await using var db = BuildDb();
        var sut = BuildSut(db);
        var sw = Stopwatch.StartNew();

        var result = await sut.Check(new CheckRequest { Email = "ghost@example.com" }, default);

        sw.Stop();
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<CheckResponse>().Subject;
        payload.AccountType.Should().Be("cloud");
        sw.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(45,
            "the constant-time envelope is 50ms; allow 5ms scheduler slop");
    }

    [Fact]
    public async Task Check_serializes_account_type_as_kebab_case()
    {
        // The mobile client decodes `account-type` not `accountType`.
        var payload = new CheckResponse { AccountType = "cloud" };
        var json = JsonSerializer.Serialize(payload);

        json.Should().Contain("\"account-type\"");
        json.Should().NotContain("accountType");
    }

    [Fact]
    public async Task Check_returns_429_when_ip_minute_limit_exceeded()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        _rateLimiter.Setup(r => r.ObserveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckRateLimitDecision(
                CheckRateLimitOutcome.IpMinuteExceeded,
                IpHourCount: 11,
                IpMinuteCount: 11,
                EmailHourCount: 1,
                RetryAfter: TimeSpan.FromMinutes(1)));

        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.Check(new CheckRequest { Email = "u@example.com" }, default);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        sut.Response.Headers["Retry-After"].ToString().Should().Be("60");
    }

    [Fact]
    public async Task Check_returns_429_when_per_email_hour_limit_exceeded()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        _rateLimiter.Setup(r => r.ObserveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CheckRateLimitDecision(
                CheckRateLimitOutcome.EmailHourExceeded,
                IpHourCount: 5,
                IpMinuteCount: 1,
                EmailHourCount: 6,
                RetryAfter: TimeSpan.FromHours(1)));

        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.Check(new CheckRequest { Email = "target@example.com" }, default);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        sut.Response.Headers["Retry-After"].ToString().Should().Be("3600");
    }

    [Fact]
    public async Task Check_returns_403_captcha_required_when_ip_hour_exceeds_50_and_no_token()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        SetRateLimitAllowed(ipHourCount: 51);
        _captcha.Setup(c => c.ValidateAsync(It.IsAny<string>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaptchaResult.Fail("missing_token"));

        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.Check(new CheckRequest { Email = "u@example.com" }, default);

        var status = result.Should().BeOfType<ObjectResult>().Subject;
        status.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        var body = status.Value;
        body.Should().NotBeNull();
        // Body shape: { error: "captcha_required", siteKey: "..." }
        body!.ToString().Should().Contain("captcha_required");
        body.ToString().Should().Contain("test-site-key");
    }

    [Fact]
    public async Task Check_returns_cloud_when_ip_hour_exceeds_50_but_captcha_token_valid()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        SetRateLimitAllowed(ipHourCount: 75);
        _captcha.Setup(c => c.ValidateAsync(It.IsAny<string>(), "check", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CaptchaResult.Pass());

        await using var db = BuildDb();
        var sut = BuildSut(db);
        sut.Request.Headers["X-Captcha-Token"] = "valid-token";

        var result = await sut.Check(new CheckRequest { Email = "u@example.com" }, default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<CheckResponse>().Subject;
        payload.AccountType.Should().Be("cloud");
    }

    [Fact]
    public async Task Check_handles_null_request_body_with_cloud_response()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        SetRateLimitAllowed();

        await using var db = BuildDb();
        var sut = BuildSut(db);

        var result = await sut.Check(request: null, default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<CheckResponse>().Subject;
        payload.AccountType.Should().Be("cloud");
    }

    [Fact]
    public async Task Check_uses_xff_first_hop_as_client_ip()
    {
        _featureFlags.Setup(f => f.IsEnabledAsync(FlagNames.CheckEndpointEnabled, default))
            .ReturnsAsync(true);
        string? observedIp = null;
        _rateLimiter
            .Setup(r => r.ObserveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((ip, _, _) => observedIp = ip)
            .ReturnsAsync(new CheckRateLimitDecision(CheckRateLimitOutcome.Allowed, 1, 1, 1, null));

        await using var db = BuildDb();
        var sut = BuildSut(db);
        sut.Request.Headers["X-Forwarded-For"] = "203.0.113.5, 10.0.0.1";

        await sut.Check(new CheckRequest { Email = "u@example.com" }, default);

        observedIp.Should().Be("203.0.113.5", "ALB-fronted services trust the first XFF hop");
    }
}
