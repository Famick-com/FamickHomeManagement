using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Web.Shared.Authentication;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// The browser app's refresh token travels in an HttpOnly cookie. Only a request carrying the
/// browser-client header gets or uses the cookie; everyone else keeps the token in the body.
/// </summary>
public class RefreshTokenCookieTests
{
    private const string CookieToken = "cookie-refresh-token";

    private readonly Mock<IAuthenticationService> _auth = new();

    private AuthApiController BuildController(HttpContext http)
    {
        var db = new HomeManagementDbContext(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"rt-cookie-{Guid.NewGuid()}").Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        return new AuthApiController(
            authService: _auth.Object,
            setupService: Mock.Of<ISetupService>(),
            passwordResetService: Mock.Of<IPasswordResetService>(),
            registrationService: Mock.Of<IRegistrationService>(),
            tokenService: Mock.Of<ITokenService>(),
            passwordHasher: Mock.Of<IPasswordHasher>(),
            passkeyService: Mock.Of<IPasskeyService>(),
            userLockService: Mock.Of<IUserAdvisoryLockService>(),
            loginThrottle: new MemoryLoginThrottle(),
            context: db,
            configuration: config,
            loginValidator: Mock.Of<IValidator<LoginRequest>>(),
            forgotPasswordValidator: Mock.Of<IValidator<ForgotPasswordRequest>>(),
            resetPasswordValidator: Mock.Of<IValidator<ResetPasswordRequest>>(),
            externalAuthSettings: Options.Create(new ExternalAuthSettings()),
            logger: NullLogger<AuthApiController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
        };
    }

    private static DefaultHttpContext Request(bool browser, string? cookie = null, string pathBase = "", bool https = false)
    {
        var http = new DefaultHttpContext();
        http.Request.PathBase = pathBase;
        http.Request.Scheme = https ? "https" : "http";
        if (browser)
            http.Request.Headers[RefreshTokenCookie.ClientHeader] = RefreshTokenCookie.BrowserClient;
        if (cookie != null)
            http.Request.Headers.Cookie = $"{RefreshTokenCookie.Name}={cookie}";
        return http;
    }

    private void RefreshSucceedsFor(string token) =>
        _auth.Setup(a => a.RefreshTokenAsync(
                It.Is<RefreshTokenRequest>(r => r.RefreshToken == token),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenResponse { AccessToken = "access", RefreshToken = "rotated" });

    private static string SetCookie(HttpContext http) => http.Response.Headers.SetCookie.ToString();

    [Fact]
    public async Task The_browser_refreshes_from_the_cookie()
    {
        RefreshSucceedsFor(CookieToken);
        var http = Request(browser: true, cookie: CookieToken);

        var result = await BuildController(http).RefreshToken(new RefreshTokenRequest(), default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task A_token_in_the_body_wins_so_an_old_browser_session_can_move_over()
    {
        RefreshSucceedsFor("legacy-token");
        var http = Request(browser: true, cookie: CookieToken);

        var result = await BuildController(http).RefreshToken(new RefreshTokenRequest { RefreshToken = "legacy-token" }, default);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task A_browser_with_no_cookie_is_not_signed_in()
    {
        var result = await BuildController(Request(browser: true)).RefreshToken(new RefreshTokenRequest(), default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task The_cookie_is_ignored_without_the_browser_client_header()
    {
        // Another site can make the browser send the cookie (SameSite aside), but cannot add a
        // custom header without a CORS preflight this server never answers.
        var result = await BuildController(Request(browser: false, cookie: CookieToken))
            .RefreshToken(new RefreshTokenRequest(), default);

        result.Should().BeOfType<BadRequestObjectResult>();
        _auth.Verify(a => a.RefreshTokenAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task A_refused_refresh_clears_the_cookie()
    {
        _auth.Setup(a => a.RefreshTokenAsync(It.IsAny<RefreshTokenRequest>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidCredentialsException("Invalid or expired refresh token"));
        var http = Request(browser: true, cookie: CookieToken);

        var result = await BuildController(http).RefreshToken(new RefreshTokenRequest(), default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
        SetCookie(http).Should().StartWith($"{RefreshTokenCookie.Name}=;").And.Contain("expires=Thu, 01 Jan 1970");
    }

    [Fact]
    public async Task Logout_revokes_the_cookie_token_and_clears_the_cookie()
    {
        var http = Request(browser: true, cookie: CookieToken);

        await BuildController(http).Logout(new RefreshTokenRequest(), default);

        _auth.Verify(a => a.RevokeRefreshTokenAsync(CookieToken, It.IsAny<CancellationToken>()), Times.Once);
        SetCookie(http).Should().StartWith($"{RefreshTokenCookie.Name}=;");
    }

    [Fact]
    public void Logout_does_not_need_an_access_token()
    {
        // Signing out has to work after the access token has expired.
        var logout = typeof(AuthApiController).GetMethod(nameof(AuthApiController.Logout))!;

        logout.IsDefined(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), inherit: true)
            .Should().BeTrue();
    }

    [Fact]
    public void A_browser_session_response_moves_the_token_into_the_cookie()
    {
        var http = Request(browser: true, pathBase: "/api/hassio_ingress/abc", https: true);
        var body = new LoginResponse { AccessToken = "access", RefreshToken = "issued" };

        RunFilter(http, body);

        body.RefreshToken.Should().BeNull("the page must never see it");
        body.AccessToken.Should().Be("access");
        var cookie = SetCookie(http);
        cookie.Should().StartWith($"{RefreshTokenCookie.Name}=issued;")
            .And.Contain("path=/api/hassio_ingress/abc/api/auth")
            .And.Contain("httponly")
            .And.Contain("samesite=strict")
            .And.Contain("secure");
    }

    [Fact]
    public void The_cookie_is_not_marked_secure_on_plain_http()
    {
        // A self-hosted server on a LAN without TLS would otherwise never get the cookie back.
        var http = Request(browser: true);

        RunFilter(http, new RefreshTokenResponse { AccessToken = "access", RefreshToken = "issued" });

        SetCookie(http).Should().NotContain("secure");
    }

    [Fact]
    public void Other_clients_keep_the_token_in_the_body()
    {
        var http = Request(browser: false);
        var body = new LoginResponse { AccessToken = "access", RefreshToken = "issued" };

        RunFilter(http, body);

        body.RefreshToken.Should().Be("issued");
        SetCookie(http).Should().BeEmpty();
    }

    private static void RunFilter(HttpContext http, object body)
    {
        var context = new ResultExecutingContext(
            new ActionContext(http, new RouteData(), new ActionDescriptor()),
            new List<IFilterMetadata>(),
            new OkObjectResult(body),
            controller: new object());

        new RefreshTokenCookieFilter().OnResultExecuting(context);
    }
}
