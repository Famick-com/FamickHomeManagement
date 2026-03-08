using Famick.HomeManagement.Core.Interfaces;
using QRCoder;

namespace Famick.HomeManagement.Web.Shared.Services;

/// <summary>
/// Service for generating QR codes for storage bins
/// </summary>
public class QrCodeService
{
    private const string CloudHost = "app.famick.com";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<QrCodeService> _logger;
    private readonly IConfiguration _configuration;

    public QrCodeService(
        IHttpContextAccessor httpContextAccessor,
        ITenantProvider tenantProvider,
        ILogger<QrCodeService> logger,
        IConfiguration configuration)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantProvider = tenantProvider;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Generates a QR code PNG for a storage bin
    /// </summary>
    /// <param name="shortCode">The storage bin short code (e.g., "blue-oak-47")</param>
    /// <param name="pixelsPerModule">Size in pixels per QR module (default 10 = ~330x330 pixels)</param>
    /// <returns>PNG image bytes</returns>
    public byte[] GenerateQrCode(string shortCode, int pixelsPerModule = 10)
    {
        var url = GetStorageBinUrl(shortCode);
        _logger.LogInformation("Generating QR code for URL: {Url}", url);

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrCodeData);

        return qrCode.GetGraphic(pixelsPerModule);
    }

    /// <summary>
    /// Generates a QR code as byte array for use in label generation
    /// </summary>
    /// <param name="shortCode">The storage bin short code</param>
    /// <param name="pixelsPerModule">Size in pixels per QR module</param>
    /// <returns>PNG image bytes</returns>
    public byte[] GenerateQrCodeBytes(string shortCode, int pixelsPerModule = 8)
    {
        return GenerateQrCode(shortCode, pixelsPerModule);
    }

    /// <summary>
    /// Gets the full URL for a storage bin. Always uses app.famick.com for deep link support.
    /// Self-hosted instances include a base64url-encoded redirect parameter.
    /// </summary>
    public string GetStorageBinUrl(string shortCode)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("TenantId is not available");

        var cloudHost = _configuration["DeepLink:CloudHost"] ?? CloudHost;
        var currentHost = request.Host.Value;

        var url = $"https://{cloudHost}/storage/{tenantId}/{shortCode}";

        // If this is NOT the cloud host, append a redirect parameter so the cloud
        // can redirect back to the self-hosted instance when the app is not installed
        if (!string.Equals(currentHost, cloudHost, StringComparison.OrdinalIgnoreCase))
        {
            var selfHostedUrl = $"{request.Scheme}://{currentHost}";
            var encoded = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(selfHostedUrl))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
            url += $"?r={encoded}";
        }

        return url;
    }

    /// <summary>
    /// Gets the base URL for the application
    /// </summary>
    public string GetBaseUrl()
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            throw new InvalidOperationException("HttpContext is not available");
        }

        return $"{request.Scheme}://{request.Host.Value}";
    }
}
