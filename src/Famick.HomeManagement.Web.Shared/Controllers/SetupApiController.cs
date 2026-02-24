using System.Web;
using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// API controller for application setup operations
/// </summary>
[ApiController]
[Route("api/setup")]
public class SetupApiController : ControllerBase
{
    private readonly ISetupService _setupService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SetupApiController> _logger;

    public SetupApiController(
        ISetupService setupService,
        IConfiguration configuration,
        ILogger<SetupApiController> logger)
    {
        _setupService = setupService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Check if initial setup is required
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Setup status indicating if setup is needed</returns>
    [HttpGet("status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SetupStatusResponse), 200)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetStatus(CancellationToken cancellationToken)
    {
        try
        {
            var status = await _setupService.GetSetupStatusAsync(cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking setup status");
            return StatusCode(500, new { error_message = "Failed to check setup status" });
        }
    }

    /// <summary>
    /// Diagnostic endpoint to check request info (for debugging proxy issues)
    /// </summary>
    [HttpGet("diagnostics")]
    [AllowAnonymous]
    [ProducesResponseType(200)]
    public IActionResult GetDiagnostics()
    {
        var headers = Request.Headers
            .Where(h => h.Key.StartsWith("X-", StringComparison.OrdinalIgnoreCase) ||
                        h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        return Ok(new
        {
            remoteIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            remotePort = HttpContext.Connection.RemotePort,
            scheme = Request.Scheme,
            host = Request.Host.ToString(),
            pathBase = Request.PathBase.ToString(),
            isHttps = Request.IsHttps,
            forwardedHeaders = headers
        });
    }

    /// <summary>
    /// Get QR code for mobile app setup
    /// </summary>
    /// <remarks>
    /// Returns a PNG image containing a QR code with a smart landing page URL.
    /// The landing page will attempt to open the app if installed, or redirect to
    /// the appropriate app store if not installed.
    /// </remarks>
    /// <param name="pixelsPerModule">Size in pixels per QR module (default 10, results in ~330x330 pixels)</param>
    /// <param name="useLandingPage">If true, use landing page URL (default). If false, use direct deep link.</param>
    /// <returns>PNG image of QR code</returns>
    [HttpGet("mobile-app/qr-code")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(FileContentResult), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public IActionResult GetMobileAppQrCode([FromQuery] int pixelsPerModule = 10, [FromQuery] bool useLandingPage = true)
    {
        try
        {
            if (!IsMobileAppSetupEnabled())
            {
                return NotFound(new { error_message = "Mobile app setup is not available for this server." });
            }

            var serverUrl = GetPublicServerUrl();
            if (string.IsNullOrEmpty(serverUrl))
            {
                return BadRequest(new { error_message = "Public URL not configured. Set 'MobileAppSetup:PublicUrl' in configuration." });
            }

            // Use landing page URL by default (enables app store redirect if app not installed)
            var qrContent = useLandingPage ? GenerateLandingPageUrl() : GenerateDeepLink();
            if (string.IsNullOrEmpty(qrContent))
            {
                return BadRequest(new { error_message = "Failed to generate QR code content." });
            }

            _logger.LogInformation("Generating mobile app QR code for URL: {Url}", qrContent);

            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrCodeData);

            var pngBytes = qrCode.GetGraphic(pixelsPerModule);
            return File(pngBytes, "image/png", "famick-setup-qr.png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating mobile app QR code");
            return StatusCode(500, new { error_message = "Failed to generate QR code" });
        }
    }

    /// <summary>
    /// Get deep link and server info for mobile app setup
    /// </summary>
    /// <remarks>
    /// Returns the deep link URL, landing page URL, and server configuration that can be shared
    /// with family members to connect their mobile app to this server.
    /// The SetupPageUrl is recommended for sharing as it will redirect to the app store
    /// if the app is not installed.
    /// </remarks>
    /// <returns>Deep link and server information</returns>
    [HttpGet("mobile-app/deep-link")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(MobileAppSetupResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public IActionResult GetMobileAppDeepLink()
    {
        try
        {
            if (!IsMobileAppSetupEnabled())
            {
                return NotFound(new { error_message = "Mobile app setup is not available for this server." });
            }

            var serverUrl = GetPublicServerUrl();
            var serverName = GetServerName();

            if (string.IsNullOrEmpty(serverUrl))
            {
                return BadRequest(new { error_message = "Public URL not configured. Set 'MobileAppSetup:PublicUrl' in configuration or configure reverse proxy headers." });
            }

            var deepLink = GenerateDeepLink();
            var setupPageUrl = GenerateLandingPageUrl();

            return Ok(new MobileAppSetupResponse
            {
                DeepLink = deepLink!,
                SetupPageUrl = setupPageUrl!,
                ServerUrl = serverUrl,
                ServerName = serverName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating mobile app deep link");
            return StatusCode(500, new { error_message = "Failed to generate deep link" });
        }
    }

    /// <summary>
    /// Get mobile app setup configuration
    /// </summary>
    /// <remarks>
    /// Returns the current server configuration for mobile app setup,
    /// including whether the feature is enabled and the public URL is properly configured.
    /// </remarks>
    /// <returns>Server configuration</returns>
    [HttpGet("mobile-app/config")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(typeof(MobileAppConfigResponse), 200)]
    public IActionResult GetMobileAppConfig()
    {
        try
        {
            var isEnabled = IsMobileAppSetupEnabled();
            var serverUrl = GetPublicServerUrl();
            var serverName = GetServerName();
            var isConfigured = isEnabled && !string.IsNullOrEmpty(serverUrl);

            return Ok(new MobileAppConfigResponse
            {
                IsEnabled = isEnabled,
                IsConfigured = isConfigured,
                ServerUrl = serverUrl,
                ServerName = serverName,
                DeepLinkScheme = "famick",
                DeepLinkHost = "setup"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting mobile app config");
            return StatusCode(500, new { error_message = "Failed to get configuration" });
        }
    }

    /// <summary>
    /// Update mobile app setup configuration
    /// </summary>
    /// <remarks>
    /// Updates the server name for mobile app setup. Note that the public URL
    /// should be configured in appsettings.json or environment variables.
    /// </remarks>
    [HttpPut("mobile-app/config")]
    [Authorize(Policy = "RequireAdmin")]
    [ProducesResponseType(200)]
    public IActionResult UpdateMobileAppConfig([FromBody] UpdateMobileAppConfigRequest request)
    {
        // Note: In a real implementation, this could persist to a database or config store
        // For now, server name and URL are read from configuration
        _logger.LogInformation("Mobile app config update requested - ServerName: {ServerName}", request.ServerName);
        return Ok(new { message = "Configuration settings should be updated in appsettings.json or environment variables." });
    }

    /// <summary>
    /// Checks if mobile app setup is enabled in configuration.
    /// </summary>
    private bool IsMobileAppSetupEnabled()
    {
        var enabledSetting = _configuration["MobileAppSetup:Enabled"];
        // Default to true if not explicitly set to false
        return string.IsNullOrEmpty(enabledSetting) ||
               !enabledSetting.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the public server URL from configuration or request headers.
    /// </summary>
    private string? GetPublicServerUrl()
    {
        // First, try configured public URL
        var configuredUrl = _configuration["MobileAppSetup:PublicUrl"];
        if (!string.IsNullOrEmpty(configuredUrl))
        {
            return configuredUrl.TrimEnd('/');
        }

        // Fall back to X-Forwarded headers (for reverse proxy)
        var forwardedProto = Request.Headers["X-Forwarded-Proto"].FirstOrDefault();
        var forwardedHost = Request.Headers["X-Forwarded-Host"].FirstOrDefault();

        if (!string.IsNullOrEmpty(forwardedHost))
        {
            var scheme = forwardedProto ?? Request.Scheme;
            return $"{scheme}://{forwardedHost}".TrimEnd('/');
        }

        // Fall back to request host (may not be publicly accessible)
        if (!string.IsNullOrEmpty(Request.Host.Value))
        {
            return $"{Request.Scheme}://{Request.Host.Value}".TrimEnd('/');
        }

        return null;
    }

    /// <summary>
    /// Gets the server name from configuration.
    /// </summary>
    private string GetServerName()
    {
        return _configuration["MobileAppSetup:ServerName"] ?? "Home Server";
    }

    /// <summary>
    /// Generates the deep link URL for mobile app setup.
    /// </summary>
    private string? GenerateDeepLink()
    {
        var serverUrl = GetPublicServerUrl();
        if (string.IsNullOrEmpty(serverUrl))
        {
            return null;
        }

        var serverName = GetServerName();
        var encodedUrl = HttpUtility.UrlEncode(serverUrl);
        var encodedName = HttpUtility.UrlEncode(serverName);

        return $"famick://setup?url={encodedUrl}&name={encodedName}";
    }

    /// <summary>
    /// Generates the landing page URL for mobile app setup.
    /// This URL will attempt to open the app if installed, or redirect to the app store if not.
    /// </summary>
    private string? GenerateLandingPageUrl()
    {
        var serverUrl = GetPublicServerUrl();
        if (string.IsNullOrEmpty(serverUrl))
        {
            return null;
        }

        var serverName = GetServerName();
        var encodedUrl = HttpUtility.UrlEncode(serverUrl);
        var encodedName = HttpUtility.UrlEncode(serverName);

        // Return the landing page URL (app-setup.html is in wwwroot)
        return $"{serverUrl}/app-setup.html?url={encodedUrl}&name={encodedName}";
    }
}

/// <summary>
/// Response containing mobile app setup deep link and server info.
/// </summary>
public class MobileAppSetupResponse
{
    /// <summary>
    /// The direct deep link URL (e.g., famick://setup?url=https://...)
    /// Use this if the user already has the app installed.
    /// </summary>
    public required string DeepLink { get; init; }

    /// <summary>
    /// The landing page URL that will attempt to open the app, or redirect to app store if not installed.
    /// This is the recommended URL to share with users who may not have the app yet.
    /// </summary>
    public required string SetupPageUrl { get; init; }

    /// <summary>
    /// The server's public URL
    /// </summary>
    public required string ServerUrl { get; init; }

    /// <summary>
    /// The server's display name
    /// </summary>
    public required string ServerName { get; init; }
}

/// <summary>
/// Response containing mobile app setup configuration.
/// </summary>
public class MobileAppConfigResponse
{
    /// <summary>
    /// Whether mobile app setup is enabled for this server.
    /// When false, the feature is hidden (e.g., for cloud-hosted servers).
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether the server is properly configured for mobile app setup
    /// (enabled and has a valid public URL)
    /// </summary>
    public bool IsConfigured { get; init; }

    /// <summary>
    /// The server's public URL, if configured
    /// </summary>
    public string? ServerUrl { get; init; }

    /// <summary>
    /// The server's display name
    /// </summary>
    public required string ServerName { get; init; }

    /// <summary>
    /// The deep link scheme (e.g., "famick")
    /// </summary>
    public required string DeepLinkScheme { get; init; }

    /// <summary>
    /// The deep link host (e.g., "setup")
    /// </summary>
    public required string DeepLinkHost { get; init; }
}

/// <summary>
/// Request to update mobile app setup configuration.
/// </summary>
public class UpdateMobileAppConfigRequest
{
    /// <summary>
    /// The server's display name
    /// </summary>
    public string? ServerName { get; init; }
}
