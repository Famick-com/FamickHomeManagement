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
/// <see cref="SpaPolicy"/>.
/// </summary>
/// <remarks>
/// <para>
/// The headers are written in <see cref="HttpResponse.OnStarting(Func{Task})"/>, after the endpoint
/// has run, so an endpoint can set its own <c>Content-Security-Policy</c> first. Server-rendered
/// pages set a strict one; stored files set <see cref="SecurityHeaders.SandboxPolicy"/>. The frame
/// rule is appended to whatever policy the endpoint chose, so no response leaves it out, unless
/// the endpoint set its own: stored files allow the app's own pages to frame them.
/// </para>
/// <para>
/// The SPA policy is enforced, so the shell (<c>Web.Client/wwwroot/index.html</c>) may load
/// scripts only from this origin: no inline blocks, no inline event handlers, no CDNs. App
/// script goes in <c>Famick.HomeManagement.UI/wwwroot/js</c>; third-party script is served from
/// <c>wwwroot/lib</c>.
/// </para>
/// </remarks>
public class SecurityHeadersMiddleware
{
    /// <summary>
    /// The policy on the Blazor WebAssembly shell.
    /// </summary>
    /// <remarks>
    /// <c>'wasm-unsafe-eval'</c> is what Blazor WebAssembly needs to compile the runtime; it does
    /// not allow JavaScript <c>eval</c>. Styles stay <c>'unsafe-inline'</c> because MudBlazor and
    /// Syncfusion write inline styles. Frames: <c>'self'</c> for the equipment document preview,
    /// OpenStreetMap for the address-verification map.
    /// </remarks>
    public const string SpaPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'wasm-unsafe-eval'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' data: https://fonts.gstatic.com; " +
        "img-src 'self' data: blob: https:; " +
        "media-src 'self' blob:; " +
        "connect-src 'self'; " +
        "frame-src 'self' https://www.openstreetmap.org; " +
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
                headers.ContentSecurityPolicy = IsHtml(response)
                    ? $"{SpaPolicy}; {_frameAncestors}"
                    : _frameAncestors;
            }
            else if (!policy.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = $"{policy.TrimEnd(' ', ';')}; {_frameAncestors}";
            }
            else if (policy.Contains("frame-ancestors 'self'", StringComparison.OrdinalIgnoreCase))
            {
                // The endpoint chose same-origin framing; keep the legacy header in step with it.
                headers.XFrameOptions = "SAMEORIGIN";
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
