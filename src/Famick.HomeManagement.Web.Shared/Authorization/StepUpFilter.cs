using System.Text.Json;
using Famick.HomeManagement.FeatureFlags;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FlagNames = Famick.HomeManagement.FeatureFlags.FeatureFlags;

namespace Famick.HomeManagement.Web.Shared.Authorization;

/// <summary>
/// Phase 2 step-up authorization filter. Runs on every action; only acts when
/// the action (or its controller) carries <see cref="StepUpAttribute"/>.
///
/// On a [StepUp] endpoint, rejects with <c>403 STEP_UP_REQUIRED</c> when the
/// access token's <c>auth_time</c> is older than the configured freshness
/// window (<c>JwtSettings:StepUpFreshnessSeconds</c>, default 300s) or when
/// the claim is missing entirely.
///
/// Gated by the <c>step_up_enabled</c> feature flag — when off, the filter is
/// a pass-through so the [StepUp] annotations are inert until rollout.
/// </summary>
public sealed class StepUpFilter : IAsyncAuthorizationFilter
{
    private readonly IFeatureFlagService _featureFlags;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StepUpFilter> _logger;

    public StepUpFilter(
        IFeatureFlagService featureFlags,
        IConfiguration configuration,
        ILogger<StepUpFilter> logger)
    {
        _featureFlags = featureFlags;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var attribute = context.ActionDescriptor.EndpointMetadata
            .OfType<StepUpAttribute>()
            .FirstOrDefault();
        if (attribute is null)
        {
            return;
        }

        if (!await _featureFlags.IsEnabledAsync(FlagNames.StepUpEnabled))
        {
            return;
        }

        var authTimeClaim = context.HttpContext.User.FindFirst("auth_time")?.Value;
        if (!long.TryParse(authTimeClaim, out var authTime))
        {
            _logger.LogInformation(
                "Step-up rejected: auth_time claim missing or unparseable on path {Path}",
                context.HttpContext.Request.Path.Value);
            context.Result = StepUpRejection();
            return;
        }

        var threshold = attribute.FreshnessSeconds > 0
            ? attribute.FreshnessSeconds
            : _configuration.GetValue("JwtSettings:StepUpFreshnessSeconds", 300);

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var age = nowSeconds - authTime;
        if (age > threshold)
        {
            _logger.LogInformation(
                "Step-up rejected: auth_time age {Age}s exceeds threshold {Threshold}s on path {Path}",
                age, threshold, context.HttpContext.Request.Path.Value);
            context.Result = StepUpRejection();
        }
    }

    private static ContentResult StepUpRejection() => new()
    {
        StatusCode = StatusCodes.Status403Forbidden,
        ContentType = "application/json",
        Content = JsonSerializer.Serialize(new
        {
            error_message = "Step-up authentication required",
            code = "STEP_UP_REQUIRED"
        })
    };
}
