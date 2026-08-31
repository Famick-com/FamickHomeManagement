using System.Security.Claims;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Turns "signing back in cancels the deletion" into something that holds for every way
/// of signing in.
/// </summary>
/// <remarks>
/// <para>
/// Sessions are issued from half a dozen places — password login, passkeys, external
/// providers, registration, HA ingress — and cancelling at each of them would leave the
/// promise resting on nobody forgetting the next one. Authentication is the single thing
/// they all lead to, so the cancellation lives here instead.
/// </para>
/// <para>
/// It also refuses members whose household is scheduled for deletion. They cannot call it
/// off — only an admin can — and letting them keep filing data into a household that is
/// about to be destroyed is worse than telling them what is happening.
/// </para>
/// <para>
/// Pipeline order: AuthN → JwtMinIatMiddleware → this → MustChangePassword →
/// MustAcceptTerms → AuthZ. It sits behind JwtMinIat so a revoked token is rejected
/// before it can be read as evidence that someone returned.
/// </para>
/// </remarks>
public class AccountDeletionMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Reachable while a household deletion is pending. An admin needs to be able to sign
    /// in and cancel, and anyone needs to be able to log out.
    /// </summary>
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/v1/account/deletion",
        "/api/auth/logout",
        "/api/auth/logout-all",
        "/api/v1/profile"
    };

    public AccountDeletionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAccountDeletionService deletionService)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        var iatClaim = context.User.FindFirst("iat")?.Value;

        if (!Guid.TryParse(userIdClaim, out var userId) || !long.TryParse(iatClaim, out var iat))
        {
            await _next(context);
            return;
        }

        var decision = await deletionService.ReconcileAuthenticatedRequestAsync(
            userId, iat, context.RequestAborted);

        if (decision == AccountAccessDecision.HouseholdDeletionPending
            && !IsAllowed(context.Request.Path.Value ?? string.Empty))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error_message = "This household is scheduled for deletion. Contact a household admin to cancel it.",
                code = "HOUSEHOLD_DELETION_PENDING"
            }));
            return;
        }

        await _next(context);
    }

    private static bool IsAllowed(string path)
    {
        if (AllowedPaths.Contains(path)) return true;

        // The step-up and social sign-in flows have to stay reachable so an admin whose
        // only credential is a passkey or a social account can get in to cancel.
        if (path.StartsWith("/api/auth/passkey/authenticate/", StringComparison.OrdinalIgnoreCase))
            return true;

        return path.StartsWith("/api/auth/external/", StringComparison.OrdinalIgnoreCase);
    }
}
