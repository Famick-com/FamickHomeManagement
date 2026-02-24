using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Middleware that blocks API requests when the authenticated user has a
/// must_change_password claim in their JWT. Only password-change, logout,
/// and profile-read endpoints are allowed through.
/// </summary>
public class MustChangePasswordMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/profile/change-password",
        "/api/auth/accept-terms",
        "/api/auth/logout",
        "/api/auth/logout-all",
        "/api/v1/profile",
    };

    public MustChangePasswordMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChange = context.User.FindFirst("must_change_password");
            if (mustChange?.Value == "true")
            {
                var path = context.Request.Path.Value ?? string.Empty;

                if (!IsAllowed(path))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/json";

                    var body = JsonSerializer.Serialize(new
                    {
                        error_message = "Password change required",
                        code = "MUST_CHANGE_PASSWORD"
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
