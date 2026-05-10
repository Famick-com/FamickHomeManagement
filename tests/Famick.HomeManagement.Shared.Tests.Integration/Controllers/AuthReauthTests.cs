using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.TestSupport.Containers;
using Famick.HomeManagement.Web.Shared.Controllers;
using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Integration.Controllers;

/// <summary>
/// Phase 2 — service-level integration tests for the new
/// <see cref="AuthApiController"/>.<c>Reauth</c> action. Mirrors the Phase 1
/// pattern (real Postgres, real services for the data-touching dependencies,
/// mocks for the uninvolved ones).
///
/// Pipeline coverage (filter activation, claim flow through AuthN, middleware
/// order) is deferred to chunk 2.6's WebApplicationFactory suite.
/// </summary>
public class AuthReauthTests : IClassFixture<PostgresContainerFixture>
{
    private readonly PostgresContainerFixture _fixture;

    public AuthReauthTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    private sealed record Harness(
        Func<AuthApiController> ControllerFactory,
        Guid UserId,
        string PlaintextPassword);

    private async Task<Harness> SeedAsync()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var email = $"reauth-{userId:N}@example.com";
        var password = "Test-Password-1!";

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Issuer"] = "https://test.famick.com",
                ["JwtSettings:Audience"] = "https://test.famick.com",
                ["JwtSettings:ExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["JwtSettings:RefreshTokenExtendedExpirationDays"] = "30",
                ["JwtSettings:StepUpFreshnessSeconds"] = "300"
            })
            .Build();
        var passwordHasher = new PasswordHasher(configuration);

        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Users.Add(new User
            {
                Id = userId,
                Email = email,
                FirstName = "Re",
                LastName = "Auth",
                PasswordHash = passwordHasher.HashPassword(password),
                TenantId = tenantId,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var signingKeyService = new JwtSigningKeyService(
            configuration, NullLogger<JwtSigningKeyService>.Instance);
        var tokenService = new TokenService(configuration, signingKeyService);
        var multiTenancyOptions = new MultiTenancyOptions { IsMultiTenantEnabled = true };

        AuthApiController BuildController()
        {
            var db = _fixture.CreateDbContext();
            var lockService = new PostgresUserAdvisoryLockService(
                db, NullLogger<PostgresUserAdvisoryLockService>.Instance);

            var controller = new AuthApiController(
                authService: Mock.Of<IAuthenticationService>(),
                setupService: Mock.Of<ISetupService>(),
                passwordResetService: Mock.Of<IPasswordResetService>(),
                registrationService: Mock.Of<IRegistrationService>(),
                tokenService: tokenService,
                passwordHasher: passwordHasher,
                userLockService: lockService,
                multiTenancyOptions: multiTenancyOptions,
                context: db,
                configuration: configuration,
                loginValidator: Mock.Of<IValidator<LoginRequest>>(),
                forgotPasswordValidator: Mock.Of<IValidator<ForgotPasswordRequest>>(),
                resetPasswordValidator: Mock.Of<IValidator<ResetPasswordRequest>>(),
                externalAuthSettings: Options.Create(new ExternalAuthSettings()),
                logger: NullLogger<AuthApiController>.Instance);

            // Simulate an authenticated request — Reauth reads userId from JWT claims.
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("sub", userId.ToString())
            }, "test"));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            return controller;
        }

        return new Harness(BuildController, userId, password);
    }

    private static long GetClaim(string token, string claimType) =>
        long.Parse(new JwtSecurityTokenHandler()
            .ReadJwtToken(token)
            .Claims.Single(c => c.Type == claimType).Value);

    [Fact]
    public async Task Valid_password_returns_fresh_access_token_with_recent_auth_time()
    {
        var harness = await SeedAsync();
        var nowSecondsBefore = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var result = await harness.ControllerFactory().Reauth(
            new ReauthRequest { Password = harness.PlaintextPassword }, default);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ReauthResponse>().Subject;
        response.AccessToken.Should().NotBeNullOrWhiteSpace();

        var authTime = GetClaim(response.AccessToken, "auth_time");
        authTime.Should().BeGreaterThanOrEqualTo(nowSecondsBefore,
            "reauth must produce a token whose auth_time reflects the new authentication");
        authTime.Should().BeLessThanOrEqualTo(DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1);
    }

    [Fact]
    public async Task Wrong_password_returns_401_no_token_issued()
    {
        var harness = await SeedAsync();

        var result = await harness.ControllerFactory().Reauth(
            new ReauthRequest { Password = "wrong-password" }, default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Empty_password_returns_400()
    {
        var harness = await SeedAsync();

        var result = await harness.ControllerFactory().Reauth(
            new ReauthRequest { Password = "" }, default);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Inactive_user_returns_401()
    {
        var harness = await SeedAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var user = await db.Users.FindAsync(harness.UserId);
            user!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var result = await harness.ControllerFactory().Reauth(
            new ReauthRequest { Password = harness.PlaintextPassword }, default);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Concurrent_reauth_for_same_user_serializes_via_advisory_lock()
    {
        // Two concurrent reauth calls for the same user must not interleave under
        // the advisory lock. Both should succeed (matching credentials), but only
        // one runs at a time. This exercises the same lock path as
        // RefreshTokenAsync / ChangePasswordAsync from Phase 1.
        var harness = await SeedAsync();

        var taskA = Task.Run(async () => await harness.ControllerFactory().Reauth(
            new ReauthRequest { Password = harness.PlaintextPassword }, default));
        var taskB = Task.Run(async () => await harness.ControllerFactory().Reauth(
            new ReauthRequest { Password = harness.PlaintextPassword }, default));

        var results = await Task.WhenAll(taskA, taskB);

        results.Should().AllSatisfy(r => r.Should().BeOfType<OkObjectResult>(),
            "both concurrent reauth attempts with valid credentials must succeed once the lock releases");
    }
}
