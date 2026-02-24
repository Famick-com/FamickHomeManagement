using System.Security.Claims;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// API controller for authentication operations
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthApiController : ControllerBase
{
    private readonly IAuthenticationService _authService;
    private readonly ISetupService _setupService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IRegistrationService _registrationService;
    private readonly ITokenService _tokenService;
    private readonly HomeManagementDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<ForgotPasswordRequest> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordRequest> _resetPasswordValidator;
    private readonly ExternalAuthSettings _externalAuthSettings;
    private readonly ILogger<AuthApiController> _logger;

    public AuthApiController(
        IAuthenticationService authService,
        ISetupService setupService,
        IPasswordResetService passwordResetService,
        IRegistrationService registrationService,
        ITokenService tokenService,
        HomeManagementDbContext context,
        IConfiguration configuration,
        IValidator<LoginRequest> loginValidator,
        IValidator<ForgotPasswordRequest> forgotPasswordValidator,
        IValidator<ResetPasswordRequest> resetPasswordValidator,
        IOptions<ExternalAuthSettings> externalAuthSettings,
        ILogger<AuthApiController> logger)
    {
        _authService = authService;
        _setupService = setupService;
        _passwordResetService = passwordResetService;
        _registrationService = registrationService;
        _tokenService = tokenService;
        _context = context;
        _configuration = configuration;
        _loginValidator = loginValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _externalAuthSettings = externalAuthSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registration response with user ID and tokens</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RegisterResponse), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        // Check if password authentication is enabled
        if (!_externalAuthSettings.PasswordAuthEnabled)
        {
            _logger.LogWarning("Password registration attempt blocked - password authentication is disabled");
            return StatusCode(403, new { error_message = "Password registration is disabled. Please use an external provider." });
        }

        // Check if registration is allowed (only when no users exist)
        var hasUsers = await _setupService.HasUsersAsync(cancellationToken);
        if (hasUsers)
        {
            _logger.LogWarning("Registration attempt blocked - users already exist");
            return StatusCode(403, new { error_message = "Registration is closed. The system has already been set up." });
        }

        // Basic validation
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { error_message = "All fields are required" });
        }

        if (request.Password != request.ConfirmPassword)
        {
            return BadRequest(new { error_message = "Passwords do not match" });
        }

        if (request.Password.Length < 8)
        {
            return BadRequest(new { error_message = "Password must be at least 8 characters" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();

        try
        {
            var response = await _authService.RegisterAsync(request, ipAddress, deviceInfo, autoLogin: true, cancellationToken);
            return StatusCode(201, response);
        }
        catch (DuplicateEntityException ex)
        {
            return Conflict(new { error_message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration");
            return StatusCode(500, new { error_message = "Registration failed. Please try again." });
        }
    }

    /// <summary>
    /// Start registration process (mobile onboarding flow).
    /// Creates a pending registration and sends a verification email.
    /// </summary>
    /// <param name="request">Household name and email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating email was sent</returns>
    [HttpPost("start-registration")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StartRegistrationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> StartRegistration(
        [FromBody] StartRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.HouseholdName))
        {
            return BadRequest(new { error_message = "Email and household name are required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        try
        {
            var response = await _registrationService.StartRegistrationAsync(
                request, ipAddress, deviceInfo, baseUrl, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during start registration");
            return StatusCode(500, new { error_message = "Failed to start registration. Please try again." });
        }
    }

    /// <summary>
    /// Verify email address using the token from the verification email.
    /// </summary>
    /// <param name="request">Verification token</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification result with email and household name</returns>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(VerifyEmailResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { error_message = "Token is required" });
        }

        var response = await _registrationService.VerifyEmailAsync(request, cancellationToken);

        if (!response.Verified)
        {
            return BadRequest(new { error_message = response.Message });
        }

        return Ok(response);
    }

    /// <summary>
    /// Complete registration by creating the user account and tenant.
    /// Requires email to be verified first.
    /// </summary>
    /// <param name="request">Password/OAuth details and user name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with tokens</returns>
    [HttpPost("complete-registration")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CompleteRegistrationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CompleteRegistration(
        [FromBody] CompleteRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        // Basic validation
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { error_message = "Token is required" });
        }

        if (string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName))
        {
            return BadRequest(new { error_message = "First name and last name are required" });
        }

        // Either password or OAuth provider is required
        if (string.IsNullOrWhiteSpace(request.Password) &&
            string.IsNullOrWhiteSpace(request.Provider))
        {
            return BadRequest(new { error_message = "Password or OAuth provider is required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();

        try
        {
            var response = await _registrationService.CompleteRegistrationAsync(
                request, ipAddress, deviceInfo, cancellationToken);

            if (!response.Success)
            {
                return BadRequest(new { error_message = response.Message });
            }

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during complete registration");
            return StatusCode(500, new { error_message = "Registration failed. Please try again." });
        }
    }

    /// <summary>
    /// Resend verification email for a pending registration.
    /// </summary>
    /// <param name="request">Email address to resend verification to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating email was sent</returns>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(StartRegistrationResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error_message = "Email is required" });
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        try
        {
            var response = await _registrationService.ResendVerificationEmailAsync(
                request.Email, baseUrl, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resending verification email");
            return StatusCode(500, new { error_message = "Failed to resend verification email. Please try again." });
        }
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with access and refresh tokens</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(403)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        // Check if password authentication is enabled
        if (!_externalAuthSettings.PasswordAuthEnabled)
        {
            _logger.LogWarning("Password login attempt blocked - password authentication is disabled");
            return StatusCode(403, new { error_message = "Password authentication is disabled. Please use an external provider." });
        }

        var validationResult = await _loginValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                error_message = "Validation failed",
                errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();

        try
        {
            var response = await _authService.LoginAsync(request, ipAddress, deviceInfo, cancellationToken);
            return Ok(response);
        }
        catch (InvalidCredentialsException)
        {
            return Unauthorized(new { error_message = "Invalid email or password" });
        }
        catch (AccountInactiveException)
        {
            return StatusCode(403, new { error_message = "Account is inactive" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login");
            return StatusCode(500, new { error_message = "Login failed. Please try again." });
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New access and refresh tokens</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RefreshTokenResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> RefreshToken(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new { error_message = "Refresh token is required" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();

        try
        {
            var response = await _authService.RefreshTokenAsync(request, ipAddress, deviceInfo, cancellationToken);
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
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { error_message = "Token refresh failed" });
        }
    }

    /// <summary>
    /// Logout (revoke refresh token)
    /// </summary>
    /// <param name="request">Refresh token to revoke</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return NoContent();
        }

        try
        {
            await _authService.RevokeRefreshTokenAsync(request.RefreshToken, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return NoContent(); // Still return success even if revocation fails
        }
    }

    /// <summary>
    /// Logout from all devices (revoke all refresh tokens)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPost("logout-all")]
    [Authorize]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> LogoutAll(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            await _authService.RevokeAllUserTokensAsync(userId.Value, cancellationToken);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout-all for user {UserId}", userId);
            return StatusCode(500, new { error_message = "Logout failed" });
        }
    }

    /// <summary>
    /// Request a password reset email
    /// </summary>
    /// <param name="request">Email address for password reset</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response (always returns success to prevent email enumeration)</returns>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ForgotPasswordResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        // Check if password authentication is enabled
        if (!_externalAuthSettings.PasswordAuthEnabled)
        {
            return StatusCode(403, new { error_message = "Password authentication is disabled." });
        }

        var validationResult = await _forgotPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                error_message = "Validation failed",
                errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var response = await _passwordResetService.RequestPasswordResetAsync(
            request, ipAddress, baseUrl, cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Validate a password reset token
    /// </summary>
    /// <param name="token">The reset token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with email if valid</returns>
    [HttpGet("validate-reset-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ValidateResetTokenResponse), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> ValidateResetToken(
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { error_message = "Token is required" });
        }

        var response = await _passwordResetService.ValidateResetTokenAsync(token, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Reset password using a valid token
    /// </summary>
    /// <param name="request">Reset password request with token and new password</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ResetPasswordResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(403)]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        // Check if password authentication is enabled
        if (!_externalAuthSettings.PasswordAuthEnabled)
        {
            return StatusCode(403, new { error_message = "Password authentication is disabled." });
        }

        var validationResult = await _resetPasswordValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(new
            {
                error_message = "Validation failed",
                errors = validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        var response = await _passwordResetService.ResetPasswordAsync(request, ipAddress, cancellationToken);

        if (!response.Success)
        {
            return BadRequest(new { error_message = response.Message });
        }

        return Ok(response);
    }

    /// <summary>
    /// Accept Terms of Service and Privacy Policy (cloud only).
    /// Records acceptance and returns fresh tokens without the must_accept_terms claim.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Login response with fresh tokens</returns>
    [HttpPost("accept-terms")]
    [Authorize]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AcceptTerms(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(new { error_message = "User ID not found in token" });
        }

        try
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.UserPermissions)
                    .ThenInclude(up => up.Permission)
                .Include(u => u.UserRoles)
                .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

            if (user == null)
            {
                return Unauthorized(new { error_message = "User not found" });
            }

            // Record terms acceptance
            var currentVersion = _configuration["LegalTerms:CurrentVersion"] ?? "2026-02-19";
            user.TermsAcceptedAt = DateTime.UtcNow;
            user.TermsVersion = currentVersion;
            user.TermsAcceptedIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            user.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Terms accepted by user {UserId}, version {Version}", userId, currentVersion);

            // Generate fresh tokens without must_accept_terms claim
            var permissions = user.UserPermissions
                .Select(up => up.Permission.Name)
                .ToList();

            var roles = user.UserRoles
                .Select(ur => ur.Role)
                .ToList();

            var accessToken = _tokenService.GenerateAccessToken(user, permissions, roles, mustAcceptTerms: false);
            var accessTokenExpiration = _tokenService.GetTokenExpiration();

            var refreshTokenString = _tokenService.GenerateRefreshToken();

            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var deviceInfo = HttpContext.Request.Headers["User-Agent"].ToString();
            var refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays", 7);

            var refreshToken = new Domain.Entities.RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = user.TenantId,
                TokenHash = HashToken(refreshTokenString),
                ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
                DeviceInfo = deviceInfo,
                IpAddress = ipAddress,
                RememberMe = false,
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync(cancellationToken);

            return Ok(new LoginResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshTokenString,
                ExpiresAt = accessTokenExpiration,
                MustChangePassword = user.MustChangePassword,
                MustAcceptTerms = false,
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    PreferredLanguage = user.PreferredLanguage,
                    Permissions = permissions
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting terms for user {UserId}", userId);
            return StatusCode(500, new { error_message = "Failed to accept terms. Please try again." });
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
    /// Hashes a token using SHA256 for secure storage
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
