using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Middleware that blocks API requests when the authenticated user has a
/// must_accept_terms claim in their JWT. Only terms-acceptance, logout,
/// and profile-read endpoints are allowed through.
///
/// Phase 2 wired this into self-hosted as well; the claim is normally only
/// set in cloud (where LegalTerms:CurrentVersion is configured), so the
/// middleware is a no-op in self-hosted unless an operator opts in.
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
        // Phase 2 — same step-up auth flows as MustChangePasswordMiddleware.
        // A passkey-only or social-only user with must_accept_terms=true must
        // still be able to authenticate to reach the accept-terms endpoint.
        "/api/auth/passkey/authenticate/options",
        "/api/auth/passkey/authenticate/verify",
        "/api/auth/reauth",
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

        // Phase 2 — social-auth step-up flow (challenge / callback / native) must
        // remain reachable so social-only users can sign in even with
        // must_accept_terms=true. Match any provider under /api/auth/external/.
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
