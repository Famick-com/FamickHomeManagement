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
        // Phase 2 — passkey-only users need to be able to reach the passkey
        // authenticate flow even with must_change_password=true so they can
        // sign in and reach the change-password endpoint at all.
        "/api/auth/passkey/authenticate/options",
        "/api/auth/passkey/authenticate/verify",
        // Phase 2 — re-auth endpoint refreshes auth_time without rotating the
        // session; never gate it behind the must-change flow it might be
        // helping the user satisfy.
        "/api/auth/reauth",
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

        // Phase 2 — social-auth step-up flow (challenge / callback / native) must
        // remain reachable so social-only users can sign in even with
        // must_change_password=true. Match any provider under /api/auth/external/.
        if (path.StartsWith("/api/auth/external/", StringComparison.OrdinalIgnoreCase)
            && (path.EndsWith("/challenge", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/callback", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith("/native", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}
