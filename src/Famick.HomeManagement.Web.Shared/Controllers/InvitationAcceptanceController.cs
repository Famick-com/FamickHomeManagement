using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// The two steps the person holding a household invitation link takes: see what they are
/// joining, then choose a password. Anonymous; the link's token is the credential. Under
/// <c>/api/auth</c> so the auth rate limits cover it.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class InvitationAcceptanceController : ControllerBase
{
    private readonly IHouseholdInvitationService _invitations;
    private readonly IValidator<AcceptInvitationRequest> _validator;
    private readonly ExternalAuthSettings _externalAuthSettings;

    public InvitationAcceptanceController(
        IHouseholdInvitationService invitations,
        IValidator<AcceptInvitationRequest> validator,
        IOptions<ExternalAuthSettings> externalAuthSettings)
    {
        _invitations = invitations;
        _validator = validator;
        _externalAuthSettings = externalAuthSettings.Value;
    }

    /// <summary>
    /// What an invitation link is for: the household, the address, and whether it can be accepted
    /// </summary>
    [HttpGet("invitation")]
    [ProducesResponseType(typeof(InvitationDetailsResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetInvitation([FromQuery] string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error_message = "Token is required" });
        }

        return Ok(await _invitations.GetDetailsAsync(token, cancellationToken));
    }

    /// <summary>
    /// Accepts an invitation: creates the account in the household with the chosen password
    /// </summary>
    [HttpPost("accept-invitation")]
    [ProducesResponseType(typeof(AcceptInvitationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptInvitationRequest request,
        CancellationToken cancellationToken)
    {
        if (!_externalAuthSettings.PasswordAuthEnabled)
        {
            return StatusCode(403, new { error_message = "Password authentication is disabled." });
        }

        var validation = await _validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error_message = "Validation failed",
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }

        var response = await _invitations.AcceptAsync(request, cancellationToken);
        return response.Success ? Ok(response) : BadRequest(new { error_message = response.Message });
    }
}
