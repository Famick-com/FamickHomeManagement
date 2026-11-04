using Famick.HomeManagement.Core.DTOs.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Web.Shared.Authentication;

/// <summary>
/// Carries the browser app's refresh token in an HttpOnly cookie instead of the response body,
/// so script on the page (an injected one included) can never read it.
/// </summary>
/// <remarks>
/// <para>
/// A client opts in by sending <see cref="ClientHeader"/>: <see cref="BrowserClient"/>. Only the
/// Blazor app does. The mobile app and server-to-server callers keep the token in the body.
/// </para>
/// <para>
/// The same header is the cross-site request check on refresh and logout: there is no CORS
/// policy, so another site cannot send a custom header, and the cookie is SameSite=Strict besides.
/// </para>
/// <para>
/// The cookie path is <c>{PathBase}/api/auth</c>, so it reaches only the auth endpoints and follows
/// the Home Assistant ingress prefix. It is Secure whenever the request arrived over HTTPS; a
/// self-hosted server on a plain-HTTP LAN would otherwise never get it back.
/// </para>
/// <para>
/// Over HTTPS the cookie is named <see cref="SecureName"/>, which the browser accepts only from a
/// secure origin, and an HTTPS request reads only that name. A host reached both ways keeps one
/// cookie per scheme: a browser will not let a plain-HTTP response replace a Secure cookie of the
/// same name, so with a single name the HTTP side could never keep a session.
/// </para>
/// </remarks>
public static class RefreshTokenCookie
{
    /// <summary>The cookie's name over plain HTTP.</summary>
    public const string Name = "famick_rt";

    /// <summary>The cookie's name over HTTPS.</summary>
    public const string SecureName = "__Secure-famick_rt";

    public const string ClientHeader = "X-Famick-Client";
    public const string BrowserClient = "web";

    public static string NameFor(HttpRequest request) => request.IsHttps ? SecureName : Name;

    public static bool IsBrowserClient(HttpRequest request) =>
        string.Equals(request.Headers[ClientHeader], BrowserClient, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refresh token from the cookie, for a browser-client request only.</summary>
    public static string? Read(HttpRequest request) =>
        IsBrowserClient(request) && request.Cookies.TryGetValue(NameFor(request), out var token) && !string.IsNullOrEmpty(token)
            ? token
            : null;

    public static void Write(HttpResponse response, string refreshToken)
    {
        // The token itself decides how long the session lasts; the cookie only has to outlive
        // the longest one (Remember Me). An expired token in a live cookie just fails to refresh.
        var days = response.HttpContext.RequestServices?.GetService<IConfiguration>()?
            .GetValue("JwtSettings:RefreshTokenExtendedExpirationDays", 30) ?? 30;

        var request = response.HttpContext.Request;
        response.Cookies.Append(NameFor(request), refreshToken, Options(request, DateTimeOffset.UtcNow.AddDays(days)));
    }

    public static void Clear(HttpResponse response)
    {
        var request = response.HttpContext.Request;
        response.Cookies.Delete(NameFor(request), Options(request, expires: null));
    }

    /// <summary>
    /// Moves the refresh token out of <paramref name="carrier"/> into the cookie, when the request
    /// is from the browser app. Returns whether it did.
    /// </summary>
    public static bool MoveToCookie(HttpContext context, IRefreshTokenCarrier carrier)
    {
        if (!IsBrowserClient(context.Request) || string.IsNullOrEmpty(carrier.RefreshToken))
            return false;

        Write(context.Response, carrier.RefreshToken);
        carrier.RefreshToken = null;
        return true;
    }

    private static CookieOptions Options(HttpRequest request, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = $"{request.PathBase}/api/auth",
        Expires = expires,
        IsEssential = true,
    };
}

/// <summary>
/// Applies <see cref="RefreshTokenCookie"/> to every action result that carries a refresh token,
/// so no endpoint that issues a session has to remember to.
/// </summary>
public class RefreshTokenCookieFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is ObjectResult { Value: IRefreshTokenCarrier carrier })
            RefreshTokenCookie.MoveToCookie(context.HttpContext, carrier);
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
