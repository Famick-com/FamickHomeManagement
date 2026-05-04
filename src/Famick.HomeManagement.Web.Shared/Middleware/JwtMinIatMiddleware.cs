using System.Security.Claims;
using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Web.Shared.Middleware;

/// <summary>
/// Destination-side JWT revocation. Reads the access token's <c>iat</c> claim and
/// rejects with <c>401 JWT_REVOKED</c> if it is earlier than the user's current
/// <c>jwt_min_iat</c>. Triggered by logout-all, password change, refresh-token
/// reuse-detection, or admin force sign-out.
///
/// Pipeline order is fixed in <c>Program.cs</c>: AuthN → JwtMinIatMiddleware →
/// MustChangePasswordMiddleware → AuthZ. The order ensures a stale JWT is rejected
/// before any allow-listed flow (change-password, accept-terms) can run.
/// </summary>
public class JwtMinIatMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMinIatMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IJwtMinIatService minIatService,
        ILogger<JwtMinIatMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        var iatClaim = context.User.FindFirst("iat")?.Value;

        if (Guid.TryParse(userIdClaim, out var userId)
            && long.TryParse(iatClaim, out var iat))
        {
            var minIat = await minIatService.GetMinIatAsync(userId, context.RequestAborted);
            if (iat < minIat)
            {
                logger.LogInformation(
                    "Rejected JWT for user {UserId}: iat {Iat} < jwt_min_iat {MinIat}",
                    userId, iat, minIat);

                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(new
                    {
                        error_message = "Token has been revoked",
                        code = "JWT_REVOKED"
                    }), context.RequestAborted);
                return;
            }
        }

        await _next(context);
    }
}
