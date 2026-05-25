using System.Diagnostics;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.FeatureFlags;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Shared.Captcha;
using Famick.HomeManagement.Shared.RateLimit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FlagNames = Famick.HomeManagement.FeatureFlags.FeatureFlags;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Phase 4 chunk 4.C — pre-login account-type probe. Mobile clients POST an
/// email and receive <c>{ "account-type": "cloud" }</c> or
/// <c>{ "account-type": "self" }</c> so they can pick the right login surface
/// (cloud login at app.famick.com vs. local-server login via proxy or LAN).
///
/// Phase 5 chunk 5.B — promoted from the cloud Web project into Web.Shared so
/// auth.famick.com (Famick.AuthProxy.Web) serves a byte-equal copy alongside
/// app.famick.com during the parallel-window cutover. Self-hosted Web ships
/// the controller dormant — the flag stays off; flip is not expected.
///
/// Contract notes (design doc "Login Process and Server API management"):
/// <list type="bullet">
/// <item>Constant-shape response — known/unknown emails return byte-identical
///       JSON. Unknown emails map to <c>cloud</c>.</item>
/// <item>Constant-time response — padded to <see cref="ConstantTimeEnvelopeMs"/>
///       before returning. Validated empirically by the 4.I CI test.</item>
/// <item>POST-only — email never appears in URL/history/referrer.</item>
/// <item>Dual rate-limit (<see cref="ICheckRateLimiter"/>) — 10/min/IP +
///       5/hr/email.</item>
/// <item>CAPTCHA gate at &gt;50 req/hr/IP — <see cref="ICaptchaService"/>.</item>
/// </list>
///
/// Phase 4 always returns <c>cloud</c> (Phase 4 chunk 4.D adds the
/// <c>User.LastDeliveredLocalServer</c> column that lights up the
/// <c>self</c> branch). Until then, the endpoint is shape-correct but
/// returns a single value, so existing emails don't leak.
/// </summary>
[ApiController]
[Route("check")]
[AllowAnonymous]
public class CheckController : ControllerBase
{
    /// <summary>Lower bound for every response. DB p99 ~5 ms; this gives
    /// ~10x headroom for jitter so unknown-vs-known timing isn't
    /// distinguishable.</summary>
    private const int ConstantTimeEnvelopeMs = 50;

    private const int CaptchaTriggerIpHourCount = 50;

    private readonly HomeManagementDbContext _db;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ICheckRateLimiter _rateLimiter;
    private readonly ICaptchaService _captcha;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CheckController> _logger;

    public CheckController(
        HomeManagementDbContext db,
        IFeatureFlagService featureFlags,
        ICheckRateLimiter rateLimiter,
        ICaptchaService captcha,
        IConfiguration configuration,
        ILogger<CheckController> logger)
    {
        _db = db;
        _featureFlags = featureFlags;
        _rateLimiter = rateLimiter;
        _captcha = captcha;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Check([FromBody] CheckRequest? request, CancellationToken cancellationToken)
    {
        if (!await _featureFlags.IsEnabledAsync(FlagNames.CheckEndpointEnabled))
            return NotFound();

        var stopwatch = Stopwatch.StartNew();

        var emailLower = (request?.Email ?? string.Empty).Trim().ToLowerInvariant();
        var ipAddress = GetClientIp();

        var rateLimit = await _rateLimiter.ObserveAsync(ipAddress, emailLower, cancellationToken);
        if (rateLimit.Outcome != CheckRateLimitOutcome.Allowed)
        {
            await PadAsync(stopwatch, cancellationToken);
            if (rateLimit.RetryAfter is { } retryAfter)
            {
                Response.Headers["Retry-After"] =
                    ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "rate_limit" });
        }

        if (rateLimit.IpHourCount > CaptchaTriggerIpHourCount)
        {
            var captchaToken = Request.Headers["X-Captcha-Token"].ToString();
            var captchaResult = await _captcha.ValidateAsync(captchaToken, action: "check", cancellationToken);
            if (!captchaResult.Success)
            {
                await PadAsync(stopwatch, cancellationToken);
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    error = "captcha_required",
                    siteKey = _configuration["RecaptchaSettings:SiteKey"] ?? string.Empty,
                });
            }
        }

        // Lookup performs the same DB roundtrip regardless of whether the
        // email exists. Result is currently ignored — Phase 4 always returns
        // "cloud"; chunk 4.D lights up the "self" branch via the new
        // User.LastDeliveredLocalServer column. Keeping the lookup wired now
        // so the constant-time envelope already accounts for the DB cost.
        if (!string.IsNullOrEmpty(emailLower))
        {
            _ = await _db.Users
                .IgnoreQueryFilters()
                .Where(u => u.Email.ToLower() == emailLower)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        await PadAsync(stopwatch, cancellationToken);
        return Ok(new CheckResponse { AccountType = "cloud" });
    }

    private static async Task PadAsync(Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var elapsed = (int)stopwatch.ElapsedMilliseconds;
        if (elapsed < ConstantTimeEnvelopeMs)
            await Task.Delay(ConstantTimeEnvelopeMs - elapsed, cancellationToken);
    }

    private string GetClientIp()
    {
        // ECS Express sits behind an ALB; the first hop in X-Forwarded-For is
        // the original client. Fall back to the direct connection for
        // dev/local where no proxy is in front.
        var xff = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(xff))
        {
            var first = xff.Split(',')[0].Trim();
            if (!string.IsNullOrEmpty(first)) return first;
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
