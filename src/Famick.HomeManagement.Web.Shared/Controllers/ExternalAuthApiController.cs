using System.Security.Claims;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// API controller for external authentication providers (Google, Apple, OIDC)
/// </summary>
[ApiController]
[Route("api/auth/external")]
public class ExternalAuthApiController : ControllerBase
{
    private readonly IExternalAuthService _externalAuthService;
    private readonly IPasskeyService _passkeyService;
    private readonly ExternalAuthSettings _settings;
    private readonly ILogger<ExternalAuthApiController> _logger;

    public ExternalAuthApiController(
        IExternalAuthService externalAuthService,
        IPasskeyService passkeyService,
        IOptions<ExternalAuthSettings> settings,
        ILogger<ExternalAuthApiController> logger)
    {
        _externalAuthService = externalAuthService;
        _passkeyService = passkeyService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets the authentication configuration for the frontend
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthConfigurationDto), 200)]
    public async Task<IActionResult> GetAuthConfiguration(CancellationToken cancellationToken)
    {
        var providers = await _externalAuthService.GetEnabledProvidersAsync(cancellationToken);

        return Ok(new AuthConfigurationDto
        {
            PasswordAuthEnabled = _settings.PasswordAuthEnabled,
            PasskeyEnabled = _passkeyService.IsEnabled,
            Providers = providers.Where(p => p.IsEnabled).ToList()
        });
    }

    /// <summary>
    /// Gets the list of enabled external authentication providers
    /// </summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(List<ExternalAuthProviderDto>), 200)]
    public async Task<IActionResult> GetProviders(CancellationToken cancellationToken)
    {
        var providers = await _externalAuthService.GetEnabledProvidersAsync(cancellationToken);
        return Ok(providers);
    }

    /// <summary>
    /// Gets the OAuth authorization URL for a provider
    /// </summary>
    /// <param name="provider">Provider name (Google, Apple, OIDC)</param>
    /// <param name="callbackUrl">Optional custom callback URL for mobile apps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("{provider}/challenge")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ExternalAuthChallengeResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetChallenge(
        string provider,
        [FromQuery] string? callbackUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var redirectUri = !string.IsNullOrWhiteSpace(callbackUrl)
                ? callbackUrl
                : GetCallbackUri(provider);
            var response = await _externalAuthService.GetAuthorizationUrlAsync(provider, redirectUri, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating OAuth challenge for {Provider}", provider);
            return StatusCode(500, new { error_message = "Failed to generate authorization URL" });
        }
    }

    /// <summary>
    /// Processes the OAuth callback and returns tokens
    /// </summary>
    /// <param name="provider">Provider name</param>
    /// <param name="request">Callback request with code and state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{provider}/callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ProcessCallback(
        string provider,
        [FromBody] ExternalAuthCallbackRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { error_message = "Authorization code is required" });
        }

        if (string.IsNullOrWhiteSpace(request.State))
        {
            return BadRequest(new { error_message = "State parameter is required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers.UserAgent.ToString();
        var redirectUri = GetCallbackUri(provider);

        try
        {
            var response = await _externalAuthService.ProcessCallbackAsync(
                provider, request, redirectUri, ipAddress, deviceInfo, cancellationToken);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            _logger.LogWarning("Invalid OAuth callback for {Provider}: {Message}", provider, ex.Message);
            return Unauthorized(new { error_message = ex.Message });
        }
        catch (AccountInactiveException)
        {
            return StatusCode(403, new { error_message = "Account is inactive" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing OAuth callback for {Provider}", provider);
            return StatusCode(500, new { error_message = "Authentication failed. Please try again." });
        }
    }

    /// <summary>
    /// Processes native Apple Sign in from iOS devices
    /// </summary>
    /// <param name="request">Request containing identity token from native Sign in with Apple</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("apple/native")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ProcessNativeAppleSignIn(
        [FromBody] NativeAppleSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            return BadRequest(new { error_message = "Identity token is required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers.UserAgent.ToString();

        try
        {
            var response = await _externalAuthService.ProcessNativeAppleSignInAsync(
                request, ipAddress, deviceInfo, cancellationToken);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            _logger.LogWarning("Invalid native Apple Sign in: {Message}", ex.Message);
            return Unauthorized(new { error_message = ex.Message });
        }
        catch (AccountInactiveException)
        {
            return StatusCode(403, new { error_message = "Account is inactive" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing native Apple Sign in");
            return StatusCode(500, new { error_message = "Authentication failed. Please try again." });
        }
    }

    /// <summary>
    /// Processes native Google Sign in from iOS and Android devices
    /// </summary>
    /// <param name="request">Request containing ID token from native Google Sign-In SDK</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("google/native")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ProcessNativeGoogleSignIn(
        [FromBody] NativeGoogleSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new { error_message = "ID token is required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers.UserAgent.ToString();

        try
        {
            var response = await _externalAuthService.ProcessNativeGoogleSignInAsync(
                request, ipAddress, deviceInfo, cancellationToken);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            _logger.LogWarning("Invalid native Google Sign in: {Message}", ex.Message);
            return Unauthorized(new { error_message = ex.Message });
        }
        catch (AccountInactiveException)
        {
            return StatusCode(403, new { error_message = "Account is inactive" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing native Google Sign in");
            return StatusCode(500, new { error_message = "Authentication failed. Please try again." });
        }
    }

    /// <summary>
    /// Gets the OAuth authorization URL for linking a provider to the current user's account
    /// </summary>
    /// <param name="provider">Provider name</param>
    /// <param name="request">Challenge request with callback URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{provider}/link")]
    [Authorize]
    [ProducesResponseType(typeof(ExternalAuthChallengeResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetLinkChallenge(
        string provider,
        [FromBody] ExternalAuthLinkChallengeRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            // Use the provided callback URL or generate one
            var redirectUri = !string.IsNullOrWhiteSpace(request.CallbackUrl)
                ? request.CallbackUrl
                : GetCallbackUri(provider);

            // Generate authorization URL with link context
            var response = await _externalAuthService.GetLinkAuthorizationUrlAsync(
                userId.Value, provider, redirectUri, cancellationToken);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating OAuth link challenge for {Provider}", provider);
            return StatusCode(500, new { error_message = "Failed to generate authorization URL" });
        }
    }

    /// <summary>
    /// Verifies OAuth callback and links an external provider to the current user's account
    /// </summary>
    /// <param name="provider">Provider name</param>
    /// <param name="request">Link request with code and state</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpPost("{provider}/link/verify")]
    [Authorize]
    [ProducesResponseType(typeof(LinkedAccountDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> VerifyLinkProvider(
        string provider,
        [FromBody] ExternalAuthLinkRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.State))
        {
            return BadRequest(new { error_message = "Code and state are required" });
        }

        var redirectUri = GetCallbackUri(provider);

        try
        {
            var result = await _externalAuthService.LinkProviderAsync(
                userId.Value, provider, request, redirectUri, cancellationToken);
            return Ok(result);
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new { error_message = ex.Message });
        }
        catch (InvalidCredentialsException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking {Provider} to user {UserId}", provider, userId);
            return StatusCode(500, new { error_message = "Failed to link account" });
        }
    }

    /// <summary>
    /// Unlinks an external provider from the current user's account
    /// </summary>
    /// <param name="provider">Provider name to unlink</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{provider}")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UnlinkProvider(
        string provider,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            await _externalAuthService.UnlinkProviderAsync(userId.Value, provider, cancellationToken);
            return NoContent();
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { error_message = $"{provider} is not linked to your account" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlinking {Provider} from user {UserId}", provider, userId);
            return StatusCode(500, new { error_message = "Failed to unlink account" });
        }
    }

    /// <summary>
    /// Gets the list of linked external accounts for the current user
    /// </summary>
    [HttpGet("linked")]
    [Authorize]
    [ProducesResponseType(typeof(List<LinkedAccountDto>), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetLinkedAccounts(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            var accounts = await _externalAuthService.GetLinkedAccountsAsync(userId.Value, cancellationToken);
            return Ok(accounts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting linked accounts for user {UserId}", userId);
            return StatusCode(500, new { error_message = "Failed to get linked accounts" });
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

    /// <summary>
    /// Mobile OAuth callback endpoint that redirects to the app's custom URL scheme.
    /// This is necessary because Google OAuth doesn't allow custom URL schemes as redirect URIs.
    /// </summary>
    /// <param name="provider">Provider name (Google, Apple, OIDC)</param>
    /// <param name="code">Authorization code from the OAuth provider</param>
    /// <param name="state">State parameter for CSRF protection</param>
    /// <param name="error">Error code if authentication failed</param>
    /// <param name="error_description">Error description if authentication failed</param>
    [HttpGet("{provider}/mobile-callback")]
    [AllowAnonymous]
    public IActionResult MobileCallback(
        string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        // Build the redirect URL to the mobile app
        const string mobileScheme = "com.famick.homemanagement";
        const string mobileHost = "oauth";
        const string mobilePath = "/callback";

        var queryParams = new List<string>();

        if (!string.IsNullOrEmpty(error))
        {
            queryParams.Add($"error={Uri.EscapeDataString(error)}");
            if (!string.IsNullOrEmpty(error_description))
            {
                queryParams.Add($"error_description={Uri.EscapeDataString(error_description)}");
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(code))
            {
                queryParams.Add($"code={Uri.EscapeDataString(code)}");
            }
            if (!string.IsNullOrEmpty(state))
            {
                queryParams.Add($"state={Uri.EscapeDataString(state)}");
            }
        }

        var redirectUrl = $"{mobileScheme}://{mobileHost}{mobilePath}";
        if (queryParams.Count > 0)
        {
            redirectUrl += "?" + string.Join("&", queryParams);
        }

        _logger.LogInformation("Mobile OAuth callback for {Provider}, redirecting to {Url}", provider, redirectUrl);

        return Redirect(redirectUrl);
    }

    /// <summary>
    /// Gets the callback URI for the specified provider
    /// </summary>
    private string GetCallbackUri(string provider)
    {
        return $"{Request.Scheme}://{Request.Host}/auth/external/callback/{provider.ToLower()}";
    }
}
