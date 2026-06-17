using System.Security.Claims;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Web.Shared.Authentication;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Controllers;

/// <summary>
/// Gating tests for POST /api/auth/ha-ingress. The endpoint mints a full
/// session, so it must fail safe: 404 when Ingress is disabled (cloud /
/// self-hosted at root), 401 unless this request was authenticated by the
/// HaIngress scheme (so a stray JWT can't mint a session), and only then issue
/// a session for the resolved user.
/// </summary>
public class HaIngressSessionEndpointTests
{
    private static AuthApiController BuildController(
        IHaIngressSessionIssuer? issuer,
        bool? enabled,
        ClaimsPrincipal user)
    {
        var db = new HomeManagementDbContext(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"ha-ep-{Guid.NewGuid()}").Options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        var settings = enabled is null
            ? null
            : new StaticMonitor(new HaIngressSettings { Enabled = enabled.Value });

        var controller = new AuthApiController(
            authService: Mock.Of<IAuthenticationService>(),
            setupService: Mock.Of<ISetupService>(),
            passwordResetService: Mock.Of<IPasswordResetService>(),
            registrationService: Mock.Of<IRegistrationService>(),
            tokenService: Mock.Of<ITokenService>(),
            passwordHasher: Mock.Of<IPasswordHasher>(),
            passkeyService: Mock.Of<IPasskeyService>(),
            userLockService: Mock.Of<IUserAdvisoryLockService>(),
            context: db,
            configuration: config,
            loginValidator: Mock.Of<IValidator<LoginRequest>>(),
            forgotPasswordValidator: Mock.Of<IValidator<ForgotPasswordRequest>>(),
            resetPasswordValidator: Mock.Of<IValidator<ResetPasswordRequest>>(),
            externalAuthSettings: Options.Create(new ExternalAuthSettings()),
            logger: NullLogger<AuthApiController>.Instance,
            multiTenancyOptions: null,
            localServerResolver: null,
            haIngressSessionIssuer: issuer,
            haIngressSettings: settings);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user },
        };
        return controller;
    }

    private static ClaimsPrincipal HaIngressUser(Guid id) =>
        new(new ClaimsIdentity(
            new[] { new Claim("sub", id.ToString()) },
            authenticationType: HaIngressAuthenticationDefaults.AuthenticationScheme));

    private static ClaimsPrincipal JwtUser(Guid id) =>
        new(new ClaimsIdentity(new[] { new Claim("sub", id.ToString()) }, authenticationType: "Bearer"));

    [Fact]
    public async Task Disabled_Returns404()
    {
        var c = BuildController(Mock.Of<IHaIngressSessionIssuer>(), enabled: false, HaIngressUser(Guid.NewGuid()));
        (await c.HaIngressSession(default)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task NoIssuerRegistered_Returns404()
    {
        // Simulates a deployment that never wired the Ingress services.
        var c = BuildController(issuer: null, enabled: true, HaIngressUser(Guid.NewGuid()));
        (await c.HaIngressSession(default)).Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Enabled_NotHaIngressScheme_Returns401()
    {
        // A JWT-authenticated caller (wrong scheme) must not mint a fresh session.
        var c = BuildController(Mock.Of<IHaIngressSessionIssuer>(), enabled: true, JwtUser(Guid.NewGuid()));
        (await c.HaIngressSession(default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Enabled_Anonymous_Returns401()
    {
        var c = BuildController(Mock.Of<IHaIngressSessionIssuer>(), enabled: true, new ClaimsPrincipal(new ClaimsIdentity()));
        (await c.HaIngressSession(default)).Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Enabled_HaIngressScheme_IssuesSessionForResolvedUser()
    {
        var userId = Guid.NewGuid();
        var issuer = new Mock<IHaIngressSessionIssuer>();
        issuer.Setup(i => i.IssueSessionAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { AccessToken = "a", RefreshToken = "r" });

        var c = BuildController(issuer.Object, enabled: true, HaIngressUser(userId));

        var result = await c.HaIngressSession(default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<LoginResponse>().Which.AccessToken.Should().Be("a");
        issuer.Verify(
            i => i.IssueSessionAsync(userId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private sealed class StaticMonitor : IOptionsMonitor<HaIngressSettings>
    {
        public StaticMonitor(HaIngressSettings value) => CurrentValue = value;
        public HaIngressSettings CurrentValue { get; }
        public HaIngressSettings Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<HaIngressSettings, string?> listener) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
