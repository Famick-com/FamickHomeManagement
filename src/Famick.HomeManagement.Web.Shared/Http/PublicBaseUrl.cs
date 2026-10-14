using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Web.Shared.Http;

/// <summary>
/// The origin that emailed and returned links point at.
/// </summary>
/// <remarks>
/// Taken from the configured <c>BaseUrl</c> (<c>https://app.famick.com</c> in the cloud), not from
/// the request. When forwarded headers are trusted, the request's host can come from a
/// client-supplied <c>X-Forwarded-Host</c>, so building links from it lets an anonymous caller have
/// a genuine password-reset email carry its token to a domain of their choosing (cloud #117).
/// The request is used only when <c>BaseUrl</c> is unset, as on a self-hosted install that is
/// reached at whatever address its owner gave it.
/// </remarks>
public static class PublicBaseUrl
{
    public static string Resolve(IConfiguration? configuration, HttpRequest request)
    {
        var configured = configuration?["BaseUrl"];
        return !string.IsNullOrWhiteSpace(configured)
            ? configured.TrimEnd('/')
            : $"{request.Scheme}://{request.Host}";
    }

    /// <summary>
    /// <see cref="Resolve(IConfiguration?, HttpRequest)"/> with configuration from the request's services.
    /// </summary>
    public static string Resolve(HttpRequest request) =>
        Resolve(request.HttpContext.RequestServices?.GetService<IConfiguration>(), request);
}
