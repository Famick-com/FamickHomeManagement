using System.Security.Claims;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// API controller for WebAuthn/FIDO2 passkey authentication
/// </summary>
[ApiController]
[Route("api/auth/passkey")]
public class PasskeyApiController : ControllerBase
{
    private readonly IPasskeyService _passkeyService;
    private readonly ILogger<PasskeyApiController> _logger;

    public PasskeyApiController(
        IPasskeyService passkeyService,
        ILogger<PasskeyApiController> logger)
    {
        _passkeyService = passkeyService;
        _logger = logger;
    }

    /// <summary>
    /// Checks if passkey authentication is enabled
    /// </summary>
    [HttpGet("enabled")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(bool), 200)]
    public IActionResult IsEnabled()
    {
        return Ok(_passkeyService.IsEnabled);
    }

    /// <summary>
    /// Gets WebAuthn registration options for creating a new passkey
    /// </summary>
    /// <param name="request">Registration options request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("register/options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyRegisterOptionsResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> GetRegisterOptions(
        [FromBody] PasskeyRegisterOptionsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        try
        {
            var response = await _passkeyService.GetRegisterOptionsAsync(userId, request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new { error_message = ex.Message });
        }
        catch (EntityNotFoundException ex)
        {
            return NotFound(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating passkey registration options");
            return StatusCode(500, new { error_message = "Failed to generate registration options" });
        }
    }

    /// <summary>
    /// Verifies and completes passkey registration
    /// </summary>
    /// <param name="request">Verification request with attestation response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("register/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyRegisterVerifyResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VerifyRegister(
        [FromBody] PasskeyRegisterVerifyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.AttestationResponse))
        {
            return BadRequest(new { error_message = "Session ID and attestation response are required" });
        }

        var userId = GetCurrentUserId();
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers.UserAgent.ToString();

        try
        {
            var response = await _passkeyService.VerifyRegisterAsync(
                userId, request, ipAddress, deviceInfo, cancellationToken);

            if (!response.Success)
            {
                return BadRequest(new { error_message = response.ErrorMessage });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying passkey registration");
            return StatusCode(500, new { error_message = "Failed to verify registration" });
        }
    }

    /// <summary>
    /// Gets WebAuthn authentication options for passkey login
    /// </summary>
    /// <param name="request">Authentication options request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("authenticate/options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyAuthenticateOptionsResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetAuthenticateOptions(
        [FromBody] PasskeyAuthenticateOptionsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _passkeyService.GetAuthenticateOptionsAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating passkey authentication options");
            return StatusCode(500, new { error_message = "Failed to generate authentication options" });
        }
    }

    /// <summary>
    /// Verifies passkey authentication and returns tokens
    /// </summary>
    /// <param name="request">Verification request with assertion response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("authenticate/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> VerifyAuthenticate(
        [FromBody] PasskeyAuthenticateVerifyRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.AssertionResponse))
        {
            return BadRequest(new { error_message = "Session ID and assertion response are required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers.UserAgent.ToString();

        try
        {
            var response = await _passkeyService.VerifyAuthenticateAsync(
                request, ipAddress, deviceInfo, cancellationToken);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { error_message = ex.Message });
        }
        catch (AccountInactiveException)
        {
            return StatusCode(403, new { error_message = "Account is inactive" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying passkey authentication");
            return StatusCode(500, new { error_message = "Authentication failed" });
        }
    }

    /// <summary>
    /// Gets the list of registered passkeys for the current user
    /// </summary>
    [HttpGet("credentials")]
    [Authorize]
    [ProducesResponseType(typeof(List<PasskeyCredentialDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetCredentials(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            var credentials = await _passkeyService.GetCredentialsAsync(userId.Value, cancellationToken);
            return Ok(credentials);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting passkey credentials for user {UserId}", userId);
            return StatusCode(500, new { error_message = "Failed to get credentials" });
        }
    }

    /// <summary>
    /// Deletes a passkey credential
    /// </summary>
    /// <param name="id">Credential ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("credentials/{id:guid}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DeleteCredential(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            await _passkeyService.DeleteCredentialAsync(userId.Value, id, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { error_message = "Credential not found" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting passkey credential {CredentialId} for user {UserId}", id, userId);
            return StatusCode(500, new { error_message = "Failed to delete credential" });
        }
    }

    /// <summary>
    /// Renames a passkey credential
    /// </summary>
    /// <param name="id">Credential ID to rename</param>
    /// <param name="request">Rename request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPut("credentials/{id:guid}/name")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RenameCredential(
        Guid id,
        [FromBody] PasskeyRenameRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        if (string.IsNullOrWhiteSpace(request.DeviceName))
        {
            return BadRequest(new { error_message = "Device name is required" });
        }

        try
        {
            await _passkeyService.RenameCredentialAsync(userId.Value, id, request, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { error_message = "Credential not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renaming passkey credential {CredentialId} for user {UserId}", id, userId);
            return StatusCode(500, new { error_message = "Failed to rename credential" });
        }
    }

    /// <summary>
    /// Gets the current user ID from the JWT claims
    /// </summary>
    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
