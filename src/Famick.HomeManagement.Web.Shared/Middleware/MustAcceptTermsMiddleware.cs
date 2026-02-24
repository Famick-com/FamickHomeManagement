using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Middleware that blocks API requests when the authenticated user has a
/// must_accept_terms claim in their JWT. Only terms-acceptance, logout,
/// and profile-read endpoints are allowed through.
/// Cloud only - the claim is never set in self-hosted mode.
/// </summary>
public class MustAcceptTermsMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/accept-terms",
        "/api/v1/profile/change-password",
        "/api/auth/logout",
        "/api/auth/logout-all",
        "/api/v1/profile",
    };

    public MustAcceptTermsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustAcceptTerms = context.User.FindFirst("must_accept_terms");
            if (mustAcceptTerms?.Value == "true")
            {
                var path = context.Request.Path.Value ?? string.Empty;

                if (!IsAllowed(path))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var body = JsonSerializer.Serialize(new
                    {
                        error_message = "Terms acceptance required",
                        code = "MUST_ACCEPT_TERMS"
                    });

                    await context.Response.WriteAsync(body);
                    return;
                }
            }
        }

        await _next(context);
    }

    private static bool IsAllowed(string path)
    {
        foreach (var allowed in AllowedPaths)
        {
            if (path.Equals(allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
