using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Web.Shared.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// When running behind HA Supervisor's Ingress reverse proxy, Supervisor
/// strips the per-request ingress prefix (<c>/api/hassio_ingress/&lt;token&gt;</c>)
/// before forwarding to the add-on and passes it back in the
/// <c>X-Ingress-Path</c> header. Setting <see cref="HttpRequest.PathBase"/>
/// from that header makes every downstream URL generator — Razor's <c>~/</c>
/// resolution, <see cref="Microsoft.AspNetCore.Mvc.IUrlHelper"/>, Blazor's
/// hub URL, OAuth redirect URLs, static-file links — produce URLs that
/// include the prefix so they keep working inside the HA sidebar iframe.
/// </summary>
/// <remarks>
/// Routing is unaffected: Supervisor has already stripped the prefix from
/// the request path, so endpoints still match against
/// <c>/api/v1/...</c>. Only URL <em>generation</em> changes.
/// Gated on <see cref="HaIngressSettings.Enabled"/> — when the add-on
/// isn't running, the header is ignored even if a client tries to spoof it.
/// </remarks>
public class HaIngressPathBaseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptionsMonitor<HaIngressSettings> _settings;

    public HaIngressPathBaseMiddleware(RequestDelegate next, IOptionsMonitor<HaIngressSettings> settings)
    {
        _next = next;
        _settings = settings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (_settings.CurrentValue.Enabled
            && context.Request.Headers.TryGetValue(HaIngressAuthenticationDefaults.IngressPathHeader, out var raw))
        {
            var pathBase = NormalizePathBase(raw.ToString());
            if (pathBase is not null)
            {
                context.Request.PathBase = new PathString(pathBase);
            }
        }
        await _next(context);
    }

    private static string? NormalizePathBase(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }
        var trimmed = raw.Trim().TrimEnd('/');
        if (trimmed.Length == 0)
        {
            return null;
        }
        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
    }
}

public static class HaIngressPathBaseMiddlewareExtensions
{
    public static IApplicationBuilder UseHaIngressPathBase(this IApplicationBuilder app)
        => app.UseMiddleware<HaIngressPathBaseMiddleware>();
}
