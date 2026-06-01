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
        // Always rewrite the URL using current ApiSettings.
        // This handles both relative URIs and absolute URIs that need to be redirected.
        if (request.RequestUri != null)
        {
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

            // Phase 5 chunk 5.J — auth traffic routes to AuthBaseUrl
            // (auth.famick.com in cloud mode when the flag is on); everything
            // else stays on BaseUrl. AuthBaseUrl == BaseUrl when the flag is
            // off or in self-hosted mode, so this is a no-op until cutover.
            var baseUrl = (IsAuthPath(path) ? _apiSettings.AuthBaseUrl : _apiSettings.BaseUrl).TrimEnd('/');

            var finalUrl = $"{baseUrl}/{path}";
            Console.WriteLine($"[DynamicApiHttpHandler] Request URL: {finalUrl}");
            request.RequestUri = new Uri(finalUrl);
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Phase 5 chunk 5.J — identifies auth-host endpoints. All <c>api/auth/*</c>
    /// controllers (login, refresh, config, challenge, callback, native sign-in,
    /// passkey, reauth, accept-terms, registration) plus the root <c>/check</c>
    /// endpoint live in Web.Shared and are served byte-equally by both the app
    /// and auth hosts, so routing them to AuthBaseUrl is transparent.
    /// </summary>
    private static bool IsAuthPath(string pathAndQuery)
    {
        var path = pathAndQuery.Split('?', 2)[0];
        return path.StartsWith("api/auth/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("check", StringComparison.OrdinalIgnoreCase);
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
