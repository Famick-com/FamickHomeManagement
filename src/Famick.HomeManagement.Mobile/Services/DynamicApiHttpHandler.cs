using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Custom HTTP handler that:
/// 1. Reads the base URL dynamically from ApiSettings for each request
/// 2. Bypasses SSL certificate validation in DEBUG mode for local development
/// </summary>
public class DynamicApiHttpHandler : HttpClientHandler
{
    private readonly ApiSettings _apiSettings;

    public DynamicApiHttpHandler(ApiSettings apiSettings)
    {
        _apiSettings = apiSettings;

#if DEBUG
        // Bypass SSL certificate validation for local development
        ServerCertificateCustomValidationCallback = BypassCertificateValidation;
#endif
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Always rewrite the URL using current ApiSettings.BaseUrl
        // This handles both relative URIs and absolute URIs that need to be redirected
        if (request.RequestUri != null)
        {
            var baseUrl = _apiSettings.BaseUrl.TrimEnd('/');

            // Extract the path from the request URI
            string path;
            if (request.RequestUri.IsAbsoluteUri)
            {
                // For absolute URIs, extract just the path and query
                path = request.RequestUri.PathAndQuery.TrimStart('/');
            }
            else
            {
                path = request.RequestUri.OriginalString.TrimStart('/');
            }

            var finalUrl = $"{baseUrl}/{path}";
            Console.WriteLine($"[DynamicApiHttpHandler] Request URL: {finalUrl}");
            request.RequestUri = new Uri(finalUrl);
        }

        return await base.SendAsync(request, cancellationToken);
    }

#if DEBUG
    private static bool BypassCertificateValidation(
        HttpRequestMessage message,
        X509Certificate2? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // In DEBUG mode, accept all certificates for local development
        // This allows self-signed localhost certificates to work
        return true;
    }
#endif
}
