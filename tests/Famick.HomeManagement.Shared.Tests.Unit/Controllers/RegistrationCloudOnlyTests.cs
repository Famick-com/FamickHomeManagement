using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
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
/// Registration is refused on a server that holds a single household.
/// <para>
/// The failure this prevents is silent rather than loud. A registration that gets through on a
/// single-tenant server writes a tenant under a fresh Guid while every query resolves the fixed
/// tenant, so the account is created, the user signs in, and the app is empty forever. Hiding
/// the entrance in the client does not close it — older builds and direct API calls still
/// arrive here.
/// </para>
/// </summary>
public class RegistrationCloudOnlyTests
{
    private static AuthApiController BuildController(
        IMultiTenancyOptions? multiTenancy,
        Mock<IRegistrationService>? registration = null)
    {
        var db = new HomeManagementDbContext(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"reg-gate-{Guid.NewGuid()}").Options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var controller = new AuthApiController(
            authService: Mock.Of<IAuthenticationService>(),
            setupService: Mock.Of<ISetupService>(),
            passwordResetService: Mock.Of<IPasswordResetService>(),
            registrationService: (registration ?? new Mock<IRegistrationService>()).Object,
            tokenService: Mock.Of<ITokenService>(),
            passwordHasher: Mock.Of<IPasswordHasher>(),
            passkeyService: Mock.Of<IPasskeyService>(),
            userLockService: Mock.Of<IUserAdvisoryLockService>(),
            context: db,
            configuration: config,
            loginValidator: Mock.Of<IValidator<LoginRequest>>(),
            forgotPasswordValidator: Mock.Of<IValidator<ForgotPasswordRequest>>(),
            resetPasswordValidator: Mock.Of<IValidator<ResetPasswordRequest>>(),
            externalAuthSettings: Options.Create(new Core.Configuration.ExternalAuthSettings()),
            logger: NullLogger<AuthApiController>.Instance,
            multiTenancyOptions: multiTenancy,
            localServerResolver: null);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static IMultiTenancyOptions SingleHousehold() =>
        new MultiTenancyOptions { IsMultiTenantEnabled = false };

    private static IMultiTenancyOptions ManyHouseholds() =>
        new MultiTenancyOptions { IsMultiTenantEnabled = true };

    private static int StatusOf(IActionResult result) =>
        (result as ObjectResult)?.StatusCode ?? 200;

    [Fact]
    public async Task StartRegistration_OnASingleHouseholdServer_IsRefused()
    {
        var registration = new Mock<IRegistrationService>();
        var controller = BuildController(SingleHousehold(), registration);

        var result = await controller.StartRegistration(
            new StartRegistrationRequest { Email = "someone@example.com", HouseholdName = "Theirs" },
            CancellationToken.None);

        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);

        // Refused before anything happens — no token written, no email sent.
        registration.Verify(
            r => r.StartRegistrationAsync(
                It.IsAny<StartRegistrationRequest>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CompleteRegistration_OnASingleHouseholdServer_IsRefused()
    {
        // The one that actually creates the unreachable household, so it must be refused even
        // if a token somehow exists from before the gate was added.
        var registration = new Mock<IRegistrationService>();
        var controller = BuildController(SingleHousehold(), registration);

        var result = await controller.CompleteRegistration(
            new CompleteRegistrationRequest { Token = "a-token", Password = "TestPassw0rd!" },
            CancellationToken.None);

        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);

        registration.Verify(
            r => r.CompleteRegistrationAsync(
                It.IsAny<CompleteRegistrationRequest>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResendVerification_OnASingleHouseholdServer_IsRefused()
    {
        var controller = BuildController(SingleHousehold());

        var result = await controller.ResendVerification(
            new ResendVerificationRequest { Email = "someone@example.com" },
            CancellationToken.None);

        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task StartRegistration_OnAServerHoldingManyHouseholds_IsAllowedThrough()
    {
        // The gate must not close on the deployment registration exists for.
        var registration = new Mock<IRegistrationService>();
        registration
            .Setup(r => r.StartRegistrationAsync(
                It.IsAny<StartRegistrationRequest>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StartRegistrationResponse { Success = true, MaskedEmail = "s****@example.com" });

        var controller = BuildController(ManyHouseholds(), registration);

        var result = await controller.StartRegistration(
            new StartRegistrationRequest { Email = "someone@example.com", HouseholdName = "Theirs" },
            CancellationToken.None);

        StatusOf(result).Should().Be(StatusCodes.Status200OK);
        registration.Verify(
            r => r.StartRegistrationAsync(
                It.IsAny<StartRegistrationRequest>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WithNoMultiTenancyBound_RegistrationIsRefusedRatherThanAssumed()
    {
        // The constructor's fallback says multi-tenant, which for a gate points the wrong way:
        // a host that forgot to bind this would permit exactly what the gate exists to refuse.
        // Unconfigured must mean closed.
        var registration = new Mock<IRegistrationService>();
        var controller = BuildController(multiTenancy: null, registration);

        var result = await controller.StartRegistration(
            new StartRegistrationRequest { Email = "someone@example.com", HouseholdName = "Theirs" },
            CancellationToken.None);

        StatusOf(result).Should().Be(StatusCodes.Status403Forbidden);
        registration.Verify(
            r => r.StartRegistrationAsync(
                It.IsAny<StartRegistrationRequest>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
