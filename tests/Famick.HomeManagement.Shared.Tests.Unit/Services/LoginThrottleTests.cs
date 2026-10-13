using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Services;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
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

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Password lockout and login enumeration (cloud #105, report findings F02 and F23).
/// <para>
/// Before this, nothing counted failed passwords, and a login for an unknown email returned
/// before paying bcrypt, so the response time said whether an account existed.
/// </para>
/// </summary>
public class LoginThrottleTests
{
    private const string Email = "resident@example.com";
    private const string Password = "correct horse battery staple";

    // ---------------------------------------------------------------------
    // Schedule
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(1, null)]
    [InlineData(5, null)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(8, 4)]
    [InlineData(9, 8)]
    [InlineData(10, 15)]
    [InlineData(1000, 15)]
    public void Policy_FiveFreeFailures_ThenDoublingFromOneMinute_CappedAtFifteen(long failures, int? minutes)
    {
        LoginThrottlePolicy.LockoutAfter(failures)
            .Should().Be(minutes is null ? null : TimeSpan.FromMinutes(minutes.Value));
    }

    [Fact]
    public void PasswordKeys_IgnoreCaseAndSpace_AndHoldNoEmail()
    {
        var key = LoginThrottleKeys.Password(" Resident@Example.com ");

        key.Should().Be(LoginThrottleKeys.Password(Email));
        key.Should().NotContainEquivalentOf("example");
    }

    // ---------------------------------------------------------------------
    // MemoryLoginThrottle
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Memory_LocksOnTheSixthFailure_AndReleasesWhenTheLockRunsOut()
    {
        var clock = new ManualClock();
        var sut = new MemoryLoginThrottle(clock);

        for (var i = 0; i < 5; i++)
        {
            (await sut.RecordFailureAsync("k")).Should().BeNull();
        }
        (await sut.GetLockoutAsync("k")).Should().BeNull();

        (await sut.RecordFailureAsync("k")).Should().Be(TimeSpan.FromMinutes(1));
        (await sut.GetLockoutAsync("k")).Should().Be(TimeSpan.FromMinutes(1));

        clock.Advance(TimeSpan.FromSeconds(61));
        (await sut.GetLockoutAsync("k")).Should().BeNull();

        // Still remembered, so the next failure escalates instead of starting over.
        (await sut.RecordFailureAsync("k")).Should().Be(TimeSpan.FromMinutes(2));
    }

    [Fact]
    public async Task Memory_ForgetsFailuresAfterTheWindow()
    {
        var clock = new ManualClock();
        var sut = new MemoryLoginThrottle(clock);

        for (var i = 0; i < 5; i++)
        {
            await sut.RecordFailureAsync("k");
        }

        clock.Advance(LoginThrottlePolicy.FailureWindow + TimeSpan.FromSeconds(1));

        (await sut.RecordFailureAsync("k")).Should().BeNull();
    }

    [Fact]
    public async Task Memory_SuccessClearsTheCount()
    {
        var sut = new MemoryLoginThrottle(new ManualClock());
        for (var i = 0; i < 5; i++)
        {
            await sut.RecordFailureAsync("k");
        }

        await sut.RecordSuccessAsync("k");

        (await sut.RecordFailureAsync("k")).Should().BeNull();
    }

    [Fact]
    public async Task Memory_KeysAreIndependent()
    {
        var sut = new MemoryLoginThrottle(new ManualClock());
        for (var i = 0; i < 6; i++)
        {
            await sut.RecordFailureAsync("a");
        }

        (await sut.GetLockoutAsync("a")).Should().NotBeNull();
        (await sut.GetLockoutAsync("b")).Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // AuthenticationService.LoginAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Login_SixthWrongPassword_LocksTheAccount_EvenForTheRightPassword()
    {
        var h = new LoginHarness();
        h.SeedUser(Email, Password);

        for (var i = 0; i < 6; i++)
        {
            await FluentActions.Awaiting(() => h.LoginAsync(Email, "wrong"))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }

        var locked = await FluentActions.Awaiting(() => h.LoginAsync(Email, Password))
            .Should().ThrowAsync<TooManyAttemptsException>();
        locked.Which.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Login_WhileLocked_DoesNotPayBcrypt()
    {
        var h = new LoginHarness();
        h.SeedUser(Email, Password);
        for (var i = 0; i < 6; i++)
        {
            await FluentActions.Awaiting(() => h.LoginAsync(Email, "wrong"))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }
        var verifiesBefore = h.Hasher.Verifies;

        await FluentActions.Awaiting(() => h.LoginAsync(Email, "wrong"))
            .Should().ThrowAsync<TooManyAttemptsException>();

        h.Hasher.Verifies.Should().Be(verifiesBefore);
    }

    [Fact]
    public async Task Login_UnknownEmail_PaysOneBcrypt_LikeAWrongPassword()
    {
        var h = new LoginHarness();

        await FluentActions.Awaiting(() => h.LoginAsync("nobody@example.com", "anything"))
            .Should().ThrowAsync<InvalidCredentialsException>();

        h.Hasher.Verifies.Should().Be(1);
        h.Hasher.LastHash.Should().StartWith("$2", "the throwaway hash is a real bcrypt hash");
    }

    [Fact]
    public async Task Login_AccountWithNoPassword_FailsLikeAWrongPassword()
    {
        // Social-only accounts have an empty hash. They must neither 500 nor answer faster.
        var h = new LoginHarness();
        h.SeedUser(Email, password: null);

        await FluentActions.Awaiting(() => h.LoginAsync(Email, "anything"))
            .Should().ThrowAsync<InvalidCredentialsException>();

        h.Hasher.Verifies.Should().Be(1);
    }

    [Fact]
    public async Task Login_UnknownEmail_LocksExactlyLikeAKnownOne()
    {
        // The lockout cannot be an existence oracle: an address with no account answers the
        // same sequence of 401s and then 429.
        var h = new LoginHarness();

        for (var i = 0; i < 6; i++)
        {
            await FluentActions.Awaiting(() => h.LoginAsync("nobody@example.com", "wrong"))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }

        await FluentActions.Awaiting(() => h.LoginAsync("nobody@example.com", "wrong"))
            .Should().ThrowAsync<TooManyAttemptsException>();
    }

    [Fact]
    public async Task Login_SuccessResetsTheCount()
    {
        var h = new LoginHarness();
        h.SeedUser(Email, Password);

        for (var i = 0; i < 5; i++)
        {
            await FluentActions.Awaiting(() => h.LoginAsync(Email, "wrong"))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }
        await h.LoginAsync(Email, Password);

        for (var i = 0; i < 5; i++)
        {
            await FluentActions.Awaiting(() => h.LoginAsync(Email, "wrong"))
                .Should().ThrowAsync<InvalidCredentialsException>();
        }
        (await h.Throttle.GetLockoutAsync(LoginThrottleKeys.Password(Email))).Should().BeNull();
    }

    // ---------------------------------------------------------------------
    // Controller
    // ---------------------------------------------------------------------

    [Fact]
    public async Task LoginEndpoint_AnswersALockoutWith429AndRetryAfter()
    {
        var auth = new Mock<IAuthenticationService>();
        auth.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TooManyAttemptsException(TimeSpan.FromSeconds(90.2)));

        var validator = new Mock<IValidator<LoginRequest>>();
        validator.Setup(v => v.ValidateAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        using var db = NewDb();
        var controller = new AuthApiController(
            authService: auth.Object,
            setupService: Mock.Of<ISetupService>(),
            passwordResetService: Mock.Of<IPasswordResetService>(),
            registrationService: Mock.Of<IRegistrationService>(),
            tokenService: Mock.Of<ITokenService>(),
            passwordHasher: Mock.Of<IPasswordHasher>(),
            passkeyService: Mock.Of<IPasskeyService>(),
            userLockService: Mock.Of<IUserAdvisoryLockService>(),
            loginThrottle: new MemoryLoginThrottle(),
            context: db,
            configuration: new ConfigurationBuilder().Build(),
            loginValidator: validator.Object,
            forgotPasswordValidator: Mock.Of<IValidator<ForgotPasswordRequest>>(),
            resetPasswordValidator: Mock.Of<IValidator<ResetPasswordRequest>>(),
            externalAuthSettings: Options.Create(new ExternalAuthSettings()),
            logger: NullLogger<AuthApiController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        var result = await controller.Login(new LoginRequest { Email = Email, Password = "x" }, default);

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(429);
        controller.Response.Headers.RetryAfter.ToString().Should().Be("91");
    }

    // ---------------------------------------------------------------------
    // Harness
    // ---------------------------------------------------------------------

    private static HomeManagementDbContext NewDb(string? name = null) =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(name ?? $"login-throttle-{Guid.NewGuid()}")
            .Options);

    private sealed class LoginHarness
    {
        private readonly string _dbName = $"login-throttle-{Guid.NewGuid()}";

        public CountingHasher Hasher { get; } = new();
        public MemoryLoginThrottle Throttle { get; } = new();

        public void SeedUser(string email, string? password)
        {
            using var db = NewDb(_dbName);
            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Email = email,
                Username = email,
                FirstName = "Resident",
                LastName = "Example",
                PasswordHash = password is null ? string.Empty : Hasher.HashPassword(password),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            db.SaveChanges();
            Hasher.Reset();
        }

        public async Task<LoginResponse> LoginAsync(string email, string password)
        {
            using var db = NewDb(_dbName);
            var tokenService = new Mock<ITokenService>();
            tokenService.Setup(t => t.GenerateAccessToken(
                    It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Role>?>(),
                    It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .Returns("access-token");
            tokenService.Setup(t => t.GenerateRefreshToken()).Returns(() => Guid.NewGuid().ToString());
            tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddMinutes(15));

            var sut = new AuthenticationService(
                db,
                Hasher,
                tokenService.Object,
                new ConfigurationBuilder().Build(),
                Mock.Of<IContactService>(),
                Mock.Of<IJwtMinIatService>(),
                Mock.Of<IUserAdvisoryLockService>(),
                Throttle,
                NullLogger<AuthenticationService>.Instance,
                new MultiTenancyOptions { IsMultiTenantEnabled = true });

            return await sut.LoginAsync(
                new LoginRequest { Email = email, Password = password }, "127.0.0.1", "tests");
        }
    }

    /// <summary>Real bcrypt at the minimum work factor, counting verifies.</summary>
    private sealed class CountingHasher : IPasswordHasher
    {
        private readonly PasswordHasher _inner = new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["BCryptWorkFactor"] = "4" })
            .Build());

        public int Verifies { get; private set; }
        public string? LastHash { get; private set; }

        public void Reset()
        {
            Verifies = 0;
            LastHash = null;
        }

        public string HashPassword(string password) => _inner.HashPassword(password);

        public bool VerifyPassword(string password, string hash)
        {
            Verifies++;
            LastHash = hash;
            return _inner.VerifyPassword(password, hash);
        }

        public bool NeedsRehash(string hash) => _inner.NeedsRehash(hash);
    }

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
