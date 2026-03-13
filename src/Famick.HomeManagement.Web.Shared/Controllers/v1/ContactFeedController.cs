using Famick.HomeManagement.Core.DTOs.Contacts;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for vCard feed generation and token management.
/// The feed endpoint is public (token-authenticated) for contact client compatibility.
/// </summary>
[ApiController]
[Route("api/v1/contacts/feed")]
[Authorize]
public class ContactFeedController : ApiControllerBase
{
    private readonly IContactFeedService _contactFeedService;

    public ContactFeedController(
        IContactFeedService contactFeedService,
        ITenantProvider tenantProvider,
        ILogger<ContactFeedController> logger)
        : base(tenantProvider, logger)
    {
        _contactFeedService = contactFeedService;
    }

    #region Public Feed Endpoint

    /// <summary>
    /// Gets the vCard feed for a token. This endpoint is NOT authenticated -
    /// it uses the URL token for contact client compatibility (macOS Contacts, Outlook, etc.).
    /// </summary>
    [HttpGet("{token}.vcf")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetFeed(
        string token,
        CancellationToken cancellationToken = default)
    {
        var vcfContent = await _contactFeedService.GenerateVcfFeedAsync(token, cancellationToken);

        if (vcfContent == null)
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

        return Content(vcfContent, "text/vcard; charset=utf-8");
    }

    #endregion

    #region Token Management

    /// <summary>
    /// Gets all vCard feed tokens for the current user
    /// </summary>
    [HttpGet("tokens")]
    [ProducesResponseType(typeof(List<UserContactVcfTokenDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetTokens(
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        var tokens = await _contactFeedService.GetTokensAsync(userId.Value, cancellationToken);

        // Compute feed URLs based on current request
        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/v1/contacts/feed";
        foreach (var token in tokens)
        {
            token.FeedUrl = $"{baseUrl}/{token.Token}.vcf";
        }

        return ApiResponse(tokens);
    }

    /// <summary>
    /// Creates a new vCard feed token for the current user
    /// </summary>
    [HttpPost("tokens")]
    [ProducesResponseType(typeof(UserContactVcfTokenDto), 201)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> CreateToken(
        [FromBody] CreateVcfTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return UnauthorizedResponse("User context not available");
        }

        _logger.LogInformation("Creating VCF feed token for user {UserId}", userId.Value);

        var token = await _contactFeedService.CreateTokenAsync(request, userId.Value, cancellationToken);

        // Compute feed URL
        var baseUrl = $"{Request.Scheme}://{Request.Host}/api/v1/contacts/feed";
        token.FeedUrl = $"{baseUrl}/{token.Token}.vcf";

        return CreatedAtAction(nameof(GetTokens), null, token);
    }

    /// <summary>
    /// Revokes a vCard feed token (feed will return 404)
    /// </summary>
    [HttpPost("tokens/{id}/revoke")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeToken(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Revoking VCF feed token {TokenId}", id);

        await _contactFeedService.RevokeTokenAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes a vCard feed token permanently
    /// </summary>
    [HttpDelete("tokens/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteToken(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting VCF feed token {TokenId}", id);

        await _contactFeedService.DeleteTokenAsync(id, cancellationToken);

        return NoContent();
    }

    #endregion

    private Guid? GetCurrentUserId()
    {
        return _tenantProvider.UserId;
    }
}
