using System.Web;
using Famick.HomeManagement.Core.DTOs.Setup;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
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
    private readonly ITenantService _tenantService;
    private readonly IMultiTenancyOptions _multiTenancyOptions;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SetupApiController> _logger;

    public SetupApiController(
        ISetupService setupService,
        ITenantService tenantService,
        IConfiguration configuration,
        ILogger<SetupApiController> logger,
        IMultiTenancyOptions? multiTenancyOptions = null)
    {
        _setupService = setupService;
        _tenantService = tenantService;
        _multiTenancyOptions = multiTenancyOptions ?? new MultiTenancyOptions { IsMultiTenantEnabled = true };
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
    /// Landing page for mobile app setup. Attempts to open the app via deep link,
    /// or offers app store downloads if the app is not installed.
    /// Generates a QR code server-side for desktop users to scan.
    /// </summary>
    [HttpGet("/app-setup")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ContentResult), 200)]
    public IActionResult GetAppSetupPage([FromQuery] string? url, [FromQuery] string? name)
    {
        var qrCodeDataUrl = string.Empty;

        if (!string.IsNullOrEmpty(url))
        {
            var serverName = name ?? "Home Server";
            var encodedUrl = HttpUtility.UrlEncode(url);
            var encodedName = HttpUtility.UrlEncode(serverName);
            var deepLink = $"famick://setup?url={encodedUrl}&name={encodedName}";

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(deepLink, QRCodeGenerator.ECCLevel.M);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var pngBytes = qrCode.GetGraphic(8);
                qrCodeDataUrl = $"data:image/png;base64,{Convert.ToBase64String(pngBytes)}";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate QR code for app setup page");
            }
        }

        var html = AppSetupHtmlTemplate.Replace("{{QR_CODE_DATA_URL}}", qrCodeDataUrl);
        return Content(html, "text/html");
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
    public async Task<IActionResult> GetMobileAppQrCode([FromQuery] int pixelsPerModule = 10, [FromQuery] bool useLandingPage = true)
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
            var qrContent = useLandingPage
                ? await GenerateLandingPageUrlAsync()
                : await GenerateDeepLinkAsync();
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
    public async Task<IActionResult> GetMobileAppDeepLink()
    {
        try
        {
            if (!IsMobileAppSetupEnabled())
            {
                return NotFound(new { error_message = "Mobile app setup is not available for this server." });
            }

            var serverUrl = GetPublicServerUrl();
            var serverName = await GetServerNameAsync();

            if (string.IsNullOrEmpty(serverUrl))
            {
                return BadRequest(new { error_message = "Public URL not configured. Set 'MobileAppSetup:PublicUrl' in configuration or configure reverse proxy headers." });
            }

            var deepLink = await GenerateDeepLinkAsync();
            var setupPageUrl = await GenerateLandingPageUrlAsync();

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
    public async Task<IActionResult> GetMobileAppConfig()
    {
        try
        {
            var isEnabled = IsMobileAppSetupEnabled();
            var serverUrl = GetPublicServerUrl();
            var serverName = await GetServerNameAsync();
            var isConfigured = isEnabled && !string.IsNullOrEmpty(serverUrl);

            return Ok(new MobileAppConfigResponse
            {
                IsEnabled = isEnabled,
                IsConfigured = isConfigured,
                IsSelfHosted = !_multiTenancyOptions.IsMultiTenantEnabled,
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
    /// Gets the server name from the tenant, then configuration, then default.
    /// </summary>
    private async Task<string> GetServerNameAsync()
    {
        try
        {
            var tenant = await _tenantService.GetCurrentTenantAsync();
            if (tenant != null && !string.IsNullOrWhiteSpace(tenant.Name))
            {
                return tenant.Name;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch tenant name for server name");
        }

        return _configuration["MobileAppSetup:ServerName"] ?? "Home Server";
    }

    /// <summary>
    /// Generates the deep link URL for mobile app setup.
    /// </summary>
    private async Task<string?> GenerateDeepLinkAsync()
    {
        var serverUrl = GetPublicServerUrl();
        if (string.IsNullOrEmpty(serverUrl))
        {
            return null;
        }

        var serverName = await GetServerNameAsync();
        var encodedUrl = HttpUtility.UrlEncode(serverUrl);
        var encodedName = HttpUtility.UrlEncode(serverName);

        return $"famick://setup?url={encodedUrl}&name={encodedName}";
    }

    /// <summary>
    /// Generates the landing page URL for mobile app setup.
    /// This URL will attempt to open the app if installed, or redirect to the app store if not.
    /// </summary>
    private async Task<string?> GenerateLandingPageUrlAsync()
    {
        var serverUrl = GetPublicServerUrl();
        if (string.IsNullOrEmpty(serverUrl))
        {
            return null;
        }

        var serverName = await GetServerNameAsync();
        var encodedUrl = HttpUtility.UrlEncode(serverUrl);
        var encodedName = HttpUtility.UrlEncode(serverName);

        return $"{serverUrl}/app-setup?url={encodedUrl}&name={encodedName}";
    }

    private const string AppSetupHtmlTemplate =
"""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Setup Famick Home Management</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, sans-serif;
            background: linear-gradient(135deg, #518751 0%, #3D6B3D 100%);
            min-height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 20px;
        }

        .container {
            background: white;
            border-radius: 16px;
            box-shadow: 0 10px 40px rgba(0, 0, 0, 0.2);
            max-width: 420px;
            width: 100%;
            padding: 32px;
            text-align: center;
        }

        .logo {
            width: 80px;
            height: 80px;
            background: linear-gradient(135deg, #7BA17C 0%, #3D6B3D 100%);
            border-radius: 20px;
            margin: 0 auto 24px;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 8px;
        }

        .logo svg { width: 100%; height: 100%; }

        h1 { font-size: 24px; color: #1a1a2e; margin-bottom: 8px; }

        .server-name { font-size: 16px; color: #666; margin-bottom: 24px; }

        .qr-section {
            margin-bottom: 24px;
            padding: 16px;
            background: #f8f9fa;
            border-radius: 12px;
        }

        .qr-section img {
            width: 200px;
            height: 200px;
            border-radius: 8px;
            image-rendering: pixelated;
        }

        .qr-section p {
            margin-top: 12px;
            font-size: 14px;
            color: #666;
        }

        .status {
            padding: 16px;
            background: #f8f9fa;
            border-radius: 12px;
            margin-bottom: 24px;
        }

        .status.loading { color: #666; }
        .status.success { background: #d4edda; color: #155724; }
        .status.redirect { background: #fff3cd; color: #856404; }

        .spinner {
            width: 24px; height: 24px;
            border: 3px solid #e0e0e0;
            border-top-color: #518751;
            border-radius: 50%;
            animation: spin 1s linear infinite;
            margin: 0 auto 12px;
        }

        @keyframes spin { to { transform: rotate(360deg); } }

        .btn {
            display: block; width: 100%;
            padding: 14px 24px; border: none; border-radius: 12px;
            font-size: 16px; font-weight: 600; cursor: pointer;
            text-decoration: none; margin-bottom: 12px;
            transition: transform 0.2s, box-shadow 0.2s;
        }

        .btn:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
        }

        .btn-primary {
            background: linear-gradient(135deg, #518751 0%, #3D6B3D 100%);
            color: white;
        }

        .btn-secondary { background: #f0f0f0; color: #333; }
        .btn-apple { background: #000; color: white; }
        .btn-google { background: #34a853; color: white; }

        .server-url {
            font-size: 12px; color: #999;
            word-break: break-all;
            margin-top: 24px; padding-top: 16px;
            border-top: 1px solid #e0e0e0;
        }

        .hidden { display: none; }

        .store-buttons { display: none; }
        .store-buttons.visible { display: block; }

        .manual-section {
            margin-top: 24px; padding-top: 24px;
            border-top: 1px solid #e0e0e0;
        }

        .manual-section h3 { font-size: 14px; color: #666; margin-bottom: 12px; }

        .copy-link { display: flex; gap: 8px; }

        .copy-link input {
            flex: 1; padding: 10px 12px;
            border: 1px solid #e0e0e0; border-radius: 8px;
            font-size: 12px; color: #666;
        }

        .copy-link button {
            padding: 10px 16px; background: #f0f0f0;
            border: none; border-radius: 8px;
            cursor: pointer; font-size: 14px;
        }

        .copy-link button:hover { background: #e0e0e0; }

        .error-message {
            background: #f8d7da; color: #721c24;
            padding: 16px; border-radius: 12px; margin-bottom: 24px;
        }
    </style>
</head>
<body>
    <div class="container">
        <div class="logo">
            <svg viewBox="348 11 262 277" xmlns="http://www.w3.org/2000/svg">
                <path fill="white" stroke="none" d="M 427.00,12.48 C 430.44,12.06 433.94,11.94 436.69,14.51 440.83,18.39 438.71,26.08 431.00,27.66 428.34,28.20 423.51,28.20 421.13,26.83 416.55,24.19 416.51,18.30 421.13,14.88 423.04,13.47 424.79,13.07 427.00,12.48 Z M 369.00,41.00 C 365.84,40.99 363.09,41.11 360.00,40.21 348.40,36.86 349.79,27.35 354.23,23.99 360.34,19.37 372.97,19.67 380.00,21.78 383.32,22.78 387.10,24.58 388.20,28.04 389.32,30.50 388.67,32.63 388.20,35.00 403.60,35.00 418.74,35.71 430.99,47.10 443.11,58.38 445.36,76.65 430.99,87.21 420.92,94.61 411.80,95.00 400.00,95.00 403.74,101.07 404.18,102.88 403.98,110.00 403.19,138.70 346.92,132.88 355.05,102.00 355.97,98.54 357.60,95.71 360.18,93.21 363.53,89.97 367.71,88.50 372.00,87.00 359.76,78.11 351.65,64.57 360.48,50.00 363.08,45.70 365.25,44.11 369.00,41.00 Z M 407.00,146.00 C 425.75,128.13 448.41,124.31 473.00,130.63 483.13,133.23 495.35,139.16 500.00,149.00 500.00,149.00 519.00,137.27 519.00,137.27 534.07,129.75 546.44,127.81 563.00,128.00 581.14,128.22 599.91,142.77 605.23,160.00 607.45,167.20 609.08,179.44 609.00,187.00 609.00,187.00 608.00,202.00 608.00,202.00 608.00,202.00 608.00,240.00 608.00,240.00 608.00,240.00 608.00,261.00 608.00,261.00 608.00,261.00 607.09,272.00 607.09,272.00 606.90,274.71 607.50,281.45 605.98,283.41 603.77,286.25 593.51,286.99 590.00,287.00 590.00,287.00 575.00,287.00 575.00,287.00 575.00,287.00 555.00,286.08 555.00,286.08 552.02,285.90 547.92,286.20 545.51,284.15 542.63,281.70 543.01,276.45 543.00,273.00 543.00,273.00 542.09,251.17 542.09,251.17 542.09,251.17 542.09,238.00 542.09,238.00 542.09,238.00 542.09,195.00 542.09,195.00 541.79,179.74 526.24,172.69 515.11,183.21 510.05,187.99 510.01,192.59 510.00,199.00 510.00,199.00 510.00,248.00 510.00,248.00 510.00,248.00 509.04,260.00 509.04,260.00 509.04,260.00 509.04,273.00 509.04,273.00 508.97,275.63 508.90,280.35 507.07,282.40 504.86,284.87 495.32,284.99 492.00,285.00 492.00,285.00 462.00,285.00 462.00,285.00 458.54,285.00 450.95,285.49 448.15,283.83 444.76,281.82 445.02,278.43 445.00,275.00 445.00,275.00 445.00,240.00 445.00,240.00 445.00,240.00 446.00,222.00 446.00,222.00 446.10,211.90 448.01,195.06 441.61,187.01 435.05,178.77 426.24,176.95 418.30,184.47 415.98,186.66 413.60,189.93 412.65,193.00 411.89,195.45 412.00,198.44 412.00,201.00 412.00,201.00 412.00,250.00 412.00,250.00 412.00,250.00 411.04,262.00 411.04,262.00 411.04,262.00 411.04,275.00 411.04,275.00 410.97,277.63 410.90,282.35 409.07,284.40 406.86,286.87 397.32,286.99 394.00,287.00 394.00,287.00 363.00,287.00 363.00,287.00 360.17,287.00 352.66,287.47 350.57,285.83 348.59,284.26 349.00,280.30 349.14,278.00 349.14,278.00 349.14,245.87 349.14,245.87 349.14,245.87 350.00,229.00 350.00,229.00 350.00,229.00 349.00,215.00 349.00,215.00 349.00,215.00 349.00,184.00 349.00,184.00 349.00,184.00 348.00,168.00 348.00,168.00 348.00,168.00 348.00,143.00 348.00,143.00 348.02,130.73 350.58,133.98 362.00,134.00 362.00,134.00 393.00,134.00 393.00,134.00 396.11,134.00 403.05,133.34 405.26,135.60 407.43,137.81 407.00,143.05 407.00,146.00 Z"/>
                <path fill="#3D6B3D" stroke="none" d="M 460.00,218.00 C 460.00,218.00 460.00,197.00 460.00,197.00 460.00,197.00 461.74,189.99 461.74,189.99 461.74,189.99 475.00,189.00 475.00,189.00 475.00,189.00 475.00,218.00 475.00,218.00 475.00,218.00 460.00,218.00 460.00,218.00 Z M 479.00,189.00 C 479.00,189.00 492.40,190.02 492.40,190.02 492.40,190.02 494.00,198.00 494.00,198.00 494.00,198.00 494.00,219.00 494.00,219.00 494.00,219.00 479.00,218.00 479.00,218.00 479.00,218.00 479.00,189.00 479.00,189.00 Z M 559.00,219.00 C 559.00,219.00 559.00,198.00 559.00,198.00 559.00,198.00 560.74,190.99 560.74,190.99 560.74,190.99 574.00,190.00 574.00,190.00 574.00,190.00 574.00,219.00 574.00,219.00 574.00,219.00 559.00,219.00 559.00,219.00 Z M 578.00,190.00 C 578.00,190.00 591.43,191.17 591.43,191.17 591.43,191.17 593.00,199.00 593.00,199.00 593.00,199.00 593.00,220.00 593.00,220.00 593.00,220.00 578.00,219.00 578.00,219.00 578.00,219.00 578.00,190.00 578.00,190.00 Z M 362.00,220.00 C 362.00,220.00 362.00,199.00 362.00,199.00 362.00,199.00 363.74,191.99 363.74,191.99 363.74,191.99 377.00,191.00 377.00,191.00 377.00,191.00 377.00,220.00 377.00,220.00 377.00,220.00 362.00,220.00 362.00,220.00 Z M 381.00,191.00 C 381.00,191.00 394.40,192.02 394.40,192.02 394.40,192.02 396.00,200.00 396.00,200.00 396.00,200.00 396.00,221.00 396.00,221.00 396.00,221.00 381.00,220.00 381.00,220.00 381.00,220.00 381.00,191.00 381.00,191.00 Z M 460.00,223.00 C 460.00,223.00 475.00,224.00 475.00,224.00 475.00,224.00 475.00,248.00 475.00,248.00 475.00,248.00 461.57,246.83 461.57,246.83 461.57,246.83 460.00,239.00 460.00,239.00 460.00,239.00 460.00,223.00 460.00,223.00 Z M 479.00,224.00 C 479.00,224.00 494.00,223.00 494.00,223.00 494.00,223.00 494.00,241.00 494.00,241.00 494.00,241.00 492.40,246.98 492.40,246.98 492.40,246.98 479.00,248.00 479.00,248.00 479.00,248.00 479.00,224.00 479.00,224.00 Z M 559.00,224.00 C 559.00,224.00 574.00,225.00 574.00,225.00 574.00,225.00 574.00,249.00 574.00,249.00 574.00,249.00 560.57,247.83 560.57,247.83 560.57,247.83 559.00,240.00 559.00,240.00 559.00,240.00 559.00,224.00 559.00,224.00 Z M 578.00,225.00 C 578.00,225.00 593.00,224.00 593.00,224.00 593.00,224.00 593.00,242.00 593.00,242.00 593.00,242.00 591.40,247.98 591.40,247.98 591.40,247.98 578.00,249.00 578.00,249.00 578.00,249.00 578.00,225.00 578.00,225.00 Z M 362.00,225.00 C 362.00,225.00 377.00,226.00 377.00,226.00 377.00,226.00 377.00,250.00 377.00,250.00 377.00,250.00 363.57,248.83 363.57,248.83 363.57,248.83 362.00,241.00 362.00,241.00 362.00,241.00 362.00,225.00 362.00,225.00 Z M 381.00,226.00 C 381.00,226.00 396.00,225.00 396.00,225.00 396.00,225.00 396.00,242.00 396.00,242.00 396.00,242.00 394.26,249.01 394.26,249.01 394.26,249.01 381.00,250.00 381.00,250.00 381.00,250.00 381.00,226.00 381.00,226.00 Z"/>
            </svg>
        </div>

        <h1>Famick Home Management</h1>
        <p class="server-name" id="serverName">Loading...</p>

        <div id="errorSection" class="error-message hidden">
            <p id="errorMessage"></p>
        </div>

        <div id="qrSection" class="qr-section hidden">
            <img id="qrCode" src="{{QR_CODE_DATA_URL}}" alt="QR Code" />
            <p>Scan this QR code with the Famick mobile app</p>
        </div>

        <div id="statusSection" class="status loading">
            <div class="spinner" id="spinner"></div>
            <p id="statusText">Opening app...</p>
        </div>

        <button id="openAppBtn" class="btn btn-primary hidden" onclick="openApp()">
            Open in App
        </button>

        <div id="storeButtons" class="store-buttons">
            <p style="color: #666; margin-bottom: 16px; font-size: 14px;">
                Don't have the app? Download it now:
            </p>
            <a id="appleStoreBtn" href="#" class="btn btn-apple hidden">
                Download on App Store
            </a>
            <a id="googleStoreBtn" href="#" class="btn btn-google hidden">
                Get it on Google Play
            </a>
        </div>

        <div class="manual-section">
            <h3>Or copy the setup link:</h3>
            <div class="copy-link">
                <input type="text" id="deepLinkInput" readonly>
                <button onclick="copyLink()">Copy</button>
            </div>
        </div>

        <p class="server-url">
            Server: <span id="serverUrl"></span>
        </p>
    </div>

    <script>
        const urlParams = new URLSearchParams(window.location.search);
        const serverUrl = urlParams.get('url');
        const serverName = urlParams.get('name') || 'Home Server';

        let config = {
            appleAppStore: '',
            googlePlayStore: '',
            deepLinkScheme: 'famick'
        };

        const serverNameEl = document.getElementById('serverName');
        const serverUrlEl = document.getElementById('serverUrl');
        const statusSection = document.getElementById('statusSection');
        const statusText = document.getElementById('statusText');
        const spinner = document.getElementById('spinner');
        const openAppBtn = document.getElementById('openAppBtn');
        const storeButtons = document.getElementById('storeButtons');
        const appleStoreBtn = document.getElementById('appleStoreBtn');
        const googleStoreBtn = document.getElementById('googleStoreBtn');
        const deepLinkInput = document.getElementById('deepLinkInput');
        const errorSection = document.getElementById('errorSection');
        const errorMessage = document.getElementById('errorMessage');
        const qrSection = document.getElementById('qrSection');
        const qrCode = document.getElementById('qrCode');

        function getPlatform() {
            const ua = navigator.userAgent.toLowerCase();
            if (/iphone|ipad|ipod/.test(ua)) return 'ios';
            if (/android/.test(ua)) return 'android';
            return 'desktop';
        }

        function getDeepLink() {
            const encodedUrl = encodeURIComponent(serverUrl);
            const encodedName = encodeURIComponent(serverName);
            return `${config.deepLinkScheme}://setup?url=${encodedUrl}&name=${encodedName}`;
        }

        function showError(message) {
            errorSection.classList.remove('hidden');
            errorMessage.textContent = message;
            statusSection.classList.add('hidden');
        }

        function openApp() {
            window.location.href = getDeepLink();
        }

        function copyLink() {
            deepLinkInput.select();
            document.execCommand('copy');
            const btn = deepLinkInput.nextElementSibling;
            const originalText = btn.textContent;
            btn.textContent = 'Copied!';
            setTimeout(() => btn.textContent = originalText, 2000);
        }

        async function init() {
            if (!serverUrl) {
                showError('Missing server URL parameter. Please use a valid setup link.');
                return;
            }

            serverNameEl.textContent = serverName;
            serverUrlEl.textContent = serverUrl;

            try {
                const response = await fetch('/api/v1/configuration/app-links');
                if (response.ok) {
                    const data = await response.json();
                    config.appleAppStore = data.appleAppStore || '';
                    config.googlePlayStore = data.googlePlayStore || '';
                    config.deepLinkScheme = data.deepLinkScheme || 'famick';
                }
            } catch (e) {
                console.log('Could not fetch app config, using defaults');
            }

            deepLinkInput.value = getDeepLink();

            const platform = getPlatform();

            const isTestFlight = config.appleAppStore && config.appleAppStore.includes('testflight.apple.com');
            if (isTestFlight) {
                appleStoreBtn.textContent = 'Join TestFlight Beta';
            }

            if (platform === 'ios' && config.appleAppStore) {
                appleStoreBtn.href = config.appleAppStore;
                appleStoreBtn.classList.remove('hidden');
            } else if (platform === 'android' && config.googlePlayStore) {
                googleStoreBtn.href = config.googlePlayStore;
                googleStoreBtn.classList.remove('hidden');
            } else if (platform === 'desktop') {
                if (config.appleAppStore) {
                    appleStoreBtn.href = config.appleAppStore;
                    appleStoreBtn.classList.remove('hidden');
                }
                if (config.googlePlayStore) {
                    googleStoreBtn.href = config.googlePlayStore;
                    googleStoreBtn.classList.remove('hidden');
                }
            }

            const hasStoreLinks = config.appleAppStore || config.googlePlayStore;

            if (platform === 'desktop') {
                // Show QR code on desktop if available
                if (qrCode && qrCode.src && !qrCode.src.endsWith('/app-setup')) {
                    qrSection.classList.remove('hidden');
                }
                spinner.classList.add('hidden');
                statusText.textContent = 'Scan the QR code with your mobile device, or copy the link below.';
                statusSection.classList.remove('loading');
                openAppBtn.classList.add('hidden');
                if (hasStoreLinks) storeButtons.classList.add('visible');
                return;
            }

            // Mobile: try to open the app
            const deepLink = getDeepLink();
            const startTime = Date.now();

            const iframe = document.createElement('iframe');
            iframe.style.display = 'none';
            iframe.src = deepLink;
            document.body.appendChild(iframe);

            setTimeout(() => { window.location.href = deepLink; }, 100);

            setTimeout(() => {
                const elapsed = Date.now() - startTime;
                if (document.visibilityState === 'visible' || elapsed > 2000) {
                    spinner.classList.add('hidden');
                    statusSection.classList.remove('loading');
                    statusSection.classList.add('redirect');
                    statusText.textContent = hasStoreLinks
                        ? 'App not detected. Download the app to continue:'
                        : 'App not detected. Copy the setup link below and open it in the app.';
                    openAppBtn.classList.remove('hidden');
                    openAppBtn.textContent = 'Try Opening App Again';
                    if (hasStoreLinks) storeButtons.classList.add('visible');
                }
            }, 2500);

            document.addEventListener('visibilitychange', () => {
                if (document.visibilityState === 'visible') {
                    setTimeout(() => {
                        if (document.visibilityState === 'visible') {
                            spinner.classList.add('hidden');
                            statusSection.classList.remove('loading');
                            statusSection.classList.add('redirect');
                            statusText.textContent = hasStoreLinks
                                ? 'Welcome back! If the app didn\'t open, download it below:'
                                : 'Welcome back! If the app didn\'t open, copy the setup link below.';
                            openAppBtn.classList.remove('hidden');
                            openAppBtn.textContent = 'Try Opening App Again';
                            if (hasStoreLinks) storeButtons.classList.add('visible');
                        }
                    }, 500);
                }
            });
        }

        init();
    </script>
</body>
</html>
""";
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
    /// Whether this is a self-hosted deployment (single-tenant mode).
    /// When false, the server is running in cloud/multi-tenant mode.
    /// </summary>
    public bool IsSelfHosted { get; init; }

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
