using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Http;

/// <summary>
/// Header values endpoints set for themselves, ahead of <see cref="Middleware.SecurityHeadersMiddleware"/>.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// For files people uploaded. If one is opened directly it runs as an opaque origin with no
    /// scripts, so an HTML or SVG file cannot act as the app.
    /// </summary>
    public const string SandboxPolicy = "sandbox";

    /// <summary>
    /// Marks a stored file as sandboxed. PDFs are left out: Chrome will not render a PDF under a
    /// sandbox policy, and its viewer does not run the document's scripts as the app anyway.
    /// </summary>
    public static void SandboxStoredFile(HttpResponse response, string? contentType)
    {
        if (!string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
            response.Headers.ContentSecurityPolicy = SandboxPolicy;
    }
}
