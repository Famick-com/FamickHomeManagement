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
/// Each scheme has its own name: <see cref="SecureName"/> over HTTPS, which the browser accepts only
/// from a secure origin, and <see cref="Name"/> over plain HTTP. A browser will not let a plain-HTTP
/// response replace a Secure cookie of the same name, so a host reached both ways could otherwise
/// never keep a session on its HTTP side. An HTTPS request reads only the secure name.
/// </para>
/// <para>
/// Both names replace <see cref="LegacyName"/>, the single name used before. A plain-HTTP request
/// still reads it, so a session started there carries on; over HTTPS it could have been set by a
/// plain-HTTP response, so it is not read. Whenever a request still carries it, the response deletes it.
/// </para>
/// </remarks>
public static class RefreshTokenCookie
{
    /// <summary>The cookie's name over plain HTTP.</summary>
    public const string Name = "famick_rt_http";

    /// <summary>The cookie's name over HTTPS.</summary>
    public const string SecureName = "__Secure-famick_rt";

    /// <summary>The name both schemes shared before each had its own.</summary>
    public const string LegacyName = "famick_rt";

    public const string ClientHeader = "X-Famick-Client";
    public const string BrowserClient = "web";

    /// <summary>The cookie's name for the scheme <paramref name="request"/> arrived over.</summary>
    public static string NameFor(HttpRequest request) => request.IsHttps ? SecureName : Name;

    /// <summary>Whether the request is from the browser app, which keeps its refresh token in the cookie.</summary>
    public static bool IsBrowserClient(HttpRequest request) =>
        string.Equals(request.Headers[ClientHeader], BrowserClient, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refresh token from the cookie, for a browser-client request only.</summary>
    public static string? Read(HttpRequest request)
    {
        if (!IsBrowserClient(request))
            return null;

        if (request.Cookies.TryGetValue(NameFor(request), out var token) && !string.IsNullOrEmpty(token))
            return token;

        return !request.IsHttps && request.Cookies.TryGetValue(LegacyName, out var legacy) && !string.IsNullOrEmpty(legacy)
            ? legacy
            : null;
    }

    /// <summary>Sets the cookie to <paramref name="refreshToken"/>, and deletes a legacy one the request carried.</summary>
    public static void Write(HttpResponse response, string refreshToken)
    {
        // The token itself decides how long the session lasts; the cookie only has to outlive
        // the longest one (Remember Me). An expired token in a live cookie just fails to refresh.
        var days = response.HttpContext.RequestServices?.GetService<IConfiguration>()?
            .GetValue("JwtSettings:RefreshTokenExtendedExpirationDays", 30) ?? 30;

        var request = response.HttpContext.Request;
        response.Cookies.Append(NameFor(request), refreshToken, Options(request, DateTimeOffset.UtcNow.AddDays(days)));
        DeleteLegacy(response);
    }

    /// <summary>Deletes the cookie, and a legacy one the request carried.</summary>
    public static void Clear(HttpResponse response)
    {
        var request = response.HttpContext.Request;
        response.Cookies.Delete(NameFor(request), Options(request, expires: null));
        DeleteLegacy(response);
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

    // Only when the request carried it, so responses don't repeat the deletion. Over HTTPS this
    // also removes a Secure legacy cookie, which the browser would otherwise keep until it expired.
    private static void DeleteLegacy(HttpResponse response)
    {
        var request = response.HttpContext.Request;
        if (request.Cookies.ContainsKey(LegacyName))
            response.Cookies.Delete(LegacyName, Options(request, expires: null));
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
