using Microsoft.AspNetCore.Builder;
using Famick.HomeManagement.Web.Shared.Http;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Who may show the app inside a frame.
/// </summary>
public enum FrameAncestors
{
    /// <summary>Nobody. The cloud app is never framed.</summary>
    None,

    /// <summary>Pages on the app's own origin. Home Assistant's sidebar frames the add-on this way.</summary>
    SameOrigin,
}

/// <summary>
/// Adds the browser hardening headers every response carries: no MIME sniffing, no referrer, and
/// a frame rule. HTML that brings no policy of its own (the Blazor SPA shell) also gets
/// <see cref="SpaReportOnlyPolicy"/> in report-only mode.
/// </summary>
/// <remarks>
/// <para>
/// The headers are written in <see cref="HttpResponse.OnStarting(Func{Task})"/>, after the endpoint
/// has run, so an endpoint can set its own <c>Content-Security-Policy</c> first. Server-rendered
/// pages set a strict one; stored files set <see cref="SecurityHeaders.SandboxPolicy"/>. The frame
/// rule is appended to whatever policy the endpoint chose, so no response leaves it out.
/// </para>
/// <para>
/// The SPA policy is report-only because the shell still has inline scripts and handlers and
/// loads the barcode scanner from unpkg. Violations show in the browser console until those move.
/// </para>
/// </remarks>
public class SecurityHeadersMiddleware
{
    /// <summary>
    /// What the Blazor WebAssembly shell would be held to once its inline scripts move out.
    /// </summary>
    public const string SpaReportOnlyPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval' https://unpkg.com; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' blob:; " +
        "connect-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    private readonly RequestDelegate _next;
    private readonly string _frameAncestors;
    private readonly string _frameOptions;

    public SecurityHeadersMiddleware(RequestDelegate next, FrameAncestors frameAncestors)
    {
        _next = next;
        (_frameAncestors, _frameOptions) = frameAncestors switch
        {
            FrameAncestors.SameOrigin => ("frame-ancestors 'self'", "SAMEORIGIN"),
            _ => ("frame-ancestors 'none'", "DENY"),
        };
    }

    public Task InvokeAsync(HttpContext context)
    {
        var response = context.Response;
        response.OnStarting(() =>
        {
            var headers = response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = _frameOptions;
            if (string.IsNullOrEmpty(headers["Referrer-Policy"]))
                headers["Referrer-Policy"] = "no-referrer";

            var policy = headers.ContentSecurityPolicy.ToString();
            if (string.IsNullOrEmpty(policy))
            {
                headers.ContentSecurityPolicy = _frameAncestors;
                if (IsHtml(response) && string.IsNullOrEmpty(headers.ContentSecurityPolicyReportOnly))
                    headers.ContentSecurityPolicyReportOnly = SpaReportOnlyPolicy;
            }
            else if (!policy.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = $"{policy.TrimEnd(' ', ';')}; {_frameAncestors}";
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }

    private static bool IsHtml(HttpResponse response) =>
        response.ContentType?.StartsWith("text/html", StringComparison.OrdinalIgnoreCase) == true;
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, FrameAncestors frameAncestors) =>
        app.UseMiddleware<SecurityHeadersMiddleware>(frameAncestors);
}
