using Famick.HomeManagement.Core.DTOs.Calendar;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for ICS calendar feed generation and token management.
/// The feed endpoint is public (token-authenticated) for calendar client compatibility.
/// </summary>
[ApiController]
[Route("api/v1/calendar/feed")]
[Authorize]
public class CalendarFeedController : ApiControllerBase
{
    private readonly ICalendarFeedService _calendarFeedService;

    public CalendarFeedController(
        ICalendarFeedService calendarFeedService,
        ITenantProvider tenantProvider,
        ILogger<CalendarFeedController> logger)
        : base(tenantProvider, logger)
    {
        _calendarFeedService = calendarFeedService;
    }

    #region Public Feed Endpoint

    /// <summary>
    /// Gets the ICS calendar feed for a token. This endpoint is NOT authenticated -
    /// it uses the URL token for calendar client compatibility (Google Calendar, Outlook, etc.).
    /// </summary>
    [HttpGet("{token}.ics")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFeed(
        string token,
        CancellationToken cancellationToken = default)
    {
        var icsContent = await _calendarFeedService.GenerateIcsFeedAsync(token, cancellationToken);

        if (icsContent == null)
        {
            return NotFound();
        }

        // Support If-Modified-Since for efficient polling
        if (Request.Headers.TryGetValue("If-Modified-Since", out var ifModifiedSince)
            && DateTime.TryParse(ifModifiedSince, out var modifiedSince)
            && modifiedSince > DateTime.UtcNow.AddMinutes(-5))
        {
            return StatusCode(304);
        }

        Response.Headers["Last-Modified"] = DateTime.UtcNow.ToString("R");
        Response.Headers["Cache-Control"] = "no-cache, must-revalidate";

        return Content(icsContent, "text/calendar; charset=utf-8");
    }

    #endregion

    #region Token Management

    /// <summary>
    /// Gets all ICS feed tokens for the current user
    /// </summary>
    [HttpGet("tokens")]
    [ProducesResponseType(typeof(List<UserCalendarIcsTokenDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetTokens(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        var tokens = await _calendarFeedService.GetTokensAsync(userId.Value, cancellationToken);

        // Compute feed URLs based on current request
        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/v1/calendar/feed";
        foreach (var token in tokens)
        {
            token.FeedUrl = $"{baseUrl}/{token.Token}.ics";
        }

        return ApiResponse(tokens);
    }

    /// <summary>
    /// Creates a new ICS feed token for the current user
    /// </summary>
    [HttpPost("tokens")]
    [ProducesResponseType(typeof(UserCalendarIcsTokenDto), 201)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateToken(
        [FromBody] CreateIcsTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        _logger.LogInformation("Creating ICS feed token for user {UserId}", userId.Value);

        var token = await _calendarFeedService.CreateTokenAsync(request, userId.Value, cancellationToken);

        // Compute feed URL
        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/v1/calendar/feed";
        token.FeedUrl = $"{baseUrl}/{token.Token}.ics";

        return CreatedAtAction(nameof(GetTokens), null, token);
    }

    /// <summary>
    /// Revokes an ICS feed token (feed will return 404)
    /// </summary>
    [HttpPost("tokens/{id}/revoke")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeToken(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revoking ICS feed token {TokenId}", id);

        await _calendarFeedService.RevokeTokenAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes an ICS feed token permanently
    /// </summary>
    [HttpDelete("tokens/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteToken(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting ICS feed token {TokenId}", id);

        await _calendarFeedService.DeleteTokenAsync(id, cancellationToken);

        return NoContent();
    }

    #endregion

    private Guid? GetCurrentUserId()
    {
        return _tenantProvider.UserId;
    }
}
