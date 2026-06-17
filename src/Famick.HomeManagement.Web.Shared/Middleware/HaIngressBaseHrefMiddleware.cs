using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Rewrites the Blazor WASM app-shell's <c>&lt;base href="/"&gt;</c> to include
/// the HA Ingress path prefix so the browser resolves <c>_framework/*</c> and
/// <c>_content/*</c> relative URLs against the add-on instead of the Home
/// Assistant root.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="HaIngressPathBaseMiddleware"/> sets <see cref="HttpRequest.PathBase"/>
/// from the <c>X-Ingress-Path</c> header, which fixes <em>server-side</em> URL
/// generation. But <c>index.html</c> is a static file shipped in the WASM
/// publish output with a hard-coded <c>&lt;base href="/"&gt;</c>; static-file
/// serving never rewrites it. Under Ingress the page loads from
/// <c>/api/hassio_ingress/&lt;token&gt;/</c>, so a root base href makes the
/// browser request <c>https://ha/_framework/blazor.webassembly.js</c> — a path
/// Supervisor does not route to the add-on — and the app hangs on the loading
/// splash. Rewriting the base href to the per-request ingress prefix is the
/// standard HA-add-on fix (the nginx equivalent is a <c>sub_filter</c> on the
/// base tag).
/// </para>
/// <para>
/// Complete no-op unless a non-empty <see cref="HttpRequest.PathBase"/> is set
/// — i.e. it only engages behind Ingress. Non-Ingress deployments (self-hosted
/// at root, cloud) never buffer a response. The prefix comes from the
/// already-validated PathBase, not from raw header text.
/// </para>
/// </remarks>
public class HaIngressBaseHrefMiddleware
{
    private readonly RequestDelegate _next;

    public HaIngressBaseHrefMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only behind Ingress (PathBase set by HaIngressPathBaseMiddleware) and
        // only for requests that can return the HTML shell. Everything else —
        // including all non-Ingress deployments — flows through untouched.
        if (!context.Request.PathBase.HasValue || !MightReturnShell(context.Request))
        {
            await _next(context);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;
        try
        {
            await _next(context);

            var contentType = context.Response.ContentType;
            var isHtml = contentType is not null
                && contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase);

            buffer.Seek(0, SeekOrigin.Begin);

            if (isHtml)
            {
                var html = await new StreamReader(buffer, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
                var prefix = context.Request.PathBase.Value!; // e.g. /api/hassio_ingress/<token>
                var rewritten = html
                    .Replace("<base href=\"/\" />", $"<base href=\"{prefix}/\" />")
                    .Replace("<base href=\"/\">", $"<base href=\"{prefix}/\">");

                var bytes = Encoding.UTF8.GetBytes(rewritten);
                // Length changed and the static ETag/Last-Modified no longer
                // describe the body we're sending — drop them so the browser
                // can't 304 its way back to a root-base-href copy.
                context.Response.ContentLength = bytes.Length;
                context.Response.Headers.Remove("ETag");
                context.Response.Headers.Remove("Last-Modified");

                context.Response.Body = originalBody;
                await originalBody.WriteAsync(bytes);
            }
            else
            {
                context.Response.Body = originalBody;
                await buffer.CopyToAsync(originalBody);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    // Cheap pre-filter so we don't buffer API/JSON/static-asset responses. The
    // SPA shell is only ever returned for GETs to "/index.html" or to
    // extensionless SPA routes (which hit MapFallbackToFile). Anything with a
    // non-.html extension, or under the API/framework/content/health/swagger
    // prefixes, can never be the shell.
    private static bool MightReturnShell(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
        {
            return false;
        }

        var path = request.Path;
        if (path.StartsWithSegments("/api")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_content")
            || path.StartsWithSegments("/health")
            || path.StartsWithSegments("/swagger"))
        {
            return false;
        }

        var value = path.Value ?? "/";
        var lastSegment = value[(value.LastIndexOf('/') + 1)..];
        var dot = lastSegment.LastIndexOf('.');
        if (dot < 0)
        {
            return true; // extensionless → SPA route → index.html fallback
        }

        return lastSegment[(dot + 1)..].Equals("html", StringComparison.OrdinalIgnoreCase);
    }
}

public static class HaIngressBaseHrefMiddlewareExtensions
{
    /// <summary>
    /// Rewrites the Blazor WASM app-shell base href to the HA Ingress prefix.
    /// Register immediately after <see cref="HaIngressPathBaseMiddlewareExtensions.UseHaIngressPathBase"/>
    /// so the PathBase it reads has already been set, and before the static-file
    /// and fallback middleware that produce the shell HTML.
    /// </summary>
    public static IApplicationBuilder UseHaIngressBaseHref(this IApplicationBuilder app)
        => app.UseMiddleware<HaIngressBaseHrefMiddleware>();
}
