using Famick.HomeManagement.Core.Interfaces;
using QRCoder;

namespace Famick.HomeManagement.Web.Shared.Services;

/// <summary>
/// Service for generating QR codes for storage bins
/// </summary>
public class QrCodeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<QrCodeService> _logger;

    public QrCodeService(
        IHttpContextAccessor httpContextAccessor,
        ITenantProvider tenantProvider,
        ILogger<QrCodeService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tenantProvider = tenantProvider;
        _logger = logger;
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
    /// Gets the full URL for a storage bin (includes tenant ID for multi-tenant support)
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

        var scheme = request.Scheme;
        var host = request.Host.Value;

        return $"{scheme}://{host}/storage/{tenantId}/{shortCode}";
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
