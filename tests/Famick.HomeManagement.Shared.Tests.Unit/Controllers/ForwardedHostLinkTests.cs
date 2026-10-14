using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// A password-reset email links to the configured origin, whatever host the request claims.
/// <para>
/// The cloud trusts forwarded headers from its load balancer, and the load balancer passes a
/// client's <c>X-Forwarded-Host</c> straight through, so <c>Request.Host</c> is whatever an
/// anonymous caller says it is. A reset link built from it hands the victim's token to the
/// caller's domain inside a genuine Famick email (cloud #117).
/// </para>
/// </summary>
public class ForwardedHostLinkTests
{
    private static AuthApiController BuildController(
        Mock<IPasswordResetService> passwordReset,
        string? configuredBaseUrl)
    {
        var db = new HomeManagementDbContext(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"forwarded-host-{Guid.NewGuid()}").Options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BaseUrl"] = configuredBaseUrl })
            .Build();

        var validator = new Mock<IValidator<ForgotPasswordRequest>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var controller = new AuthApiController(
            authService: Mock.Of<IAuthenticationService>(),
            setupService: Mock.Of<ISetupService>(),
            passwordResetService: passwordReset.Object,
            registrationService: Mock.Of<IRegistrationService>(),
            tokenService: Mock.Of<ITokenService>(),
            passwordHasher: Mock.Of<IPasswordHasher>(),
            passkeyService: Mock.Of<IPasskeyService>(),
            userLockService: Mock.Of<IUserAdvisoryLockService>(),
            loginThrottle: new MemoryLoginThrottle(),
            context: db,
            configuration: config,
            loginValidator: Mock.Of<IValidator<LoginRequest>>(),
            forgotPasswordValidator: validator.Object,
            resetPasswordValidator: Mock.Of<IValidator<ResetPasswordRequest>>(),
            externalAuthSettings: Options.Create(new Core.Configuration.ExternalAuthSettings()),
            logger: NullLogger<AuthApiController>.Instance,
            multiTenancyOptions: new MultiTenancyOptions { IsMultiTenantEnabled = true },
            localServerResolver: null);

        // What UseForwardedHeaders leaves behind for "X-Forwarded-Host: evil.example".
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("evil.example");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        return controller;
    }

    private static async Task<string?> BaseUrlSentFor(string? configuredBaseUrl)
    {
        string? sent = null;
        var passwordReset = new Mock<IPasswordResetService>();
        passwordReset
            .Setup(s => s.RequestPasswordResetAsync(
                It.IsAny<ForgotPasswordRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<ForgotPasswordRequest, string, string, CancellationToken>((_, _, baseUrl, _) => sent = baseUrl)
            .ReturnsAsync(new ForgotPasswordResponse());

        var controller = BuildController(passwordReset, configuredBaseUrl);
        await controller.ForgotPassword(
            new ForgotPasswordRequest { Email = "victim@example.com" }, CancellationToken.None);

        return sent;
    }

    [Fact]
    public async Task ForgotPassword_WithASpoofedForwardedHost_LinksToTheConfiguredBaseUrl()
    {
        var sent = await BaseUrlSentFor("https://app.famick.com/");

        sent.Should().Be("https://app.famick.com");
    }

    [Fact]
    public async Task ForgotPassword_WithNoConfiguredBaseUrl_FallsBackToTheRequest()
    {
        // Self-hosted leaves BaseUrl unset and is reached at whatever address its owner chose.
        var sent = await BaseUrlSentFor(null);

        sent.Should().Be("https://evil.example");
    }
}
