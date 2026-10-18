using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators;
using Famick.HomeManagement.Core.Validators.Common;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Validators.Common;

/// <summary>
/// The shared password rule, and the paths that set a password without going through a request
/// validator first: changing your own and first-run registration. The admin, reset-link and
/// invitation paths are covered beside their services' other tests.
/// </summary>
public class PasswordRulesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("            ")]
    [InlineData("a1!")]
    [InlineData("abcdefgh12!")]                       // 11 characters
    [InlineData("correcthorsebattery!")]              // no digit
    [InlineData("correcthorsebattery9")]              // no special character
    [InlineData("correct horse battery 9")]           // a space is not a special character
    [InlineData("correcthorsebattery9\u00e9\u00a3")]  // nor is a non-ASCII symbol
    public void Passwords_that_miss_a_requirement_are_not_acceptable(string? password)
    {
        PasswordRules.IsAcceptable(password).Should().BeFalse();
    }

    [Theory]
    [InlineData("abcdefghi12!")]                      // exactly 12
    [InlineData("correct horse battery 9~")]
    [InlineData("9{correcthorsebattery")]
    [InlineData("\u00e9t\u00e9-2026-\u00e9t\u00e9")]           // non-ASCII letters are fine
    public void Passwords_that_meet_every_requirement_are_acceptable(string password)
    {
        PasswordRules.IsAcceptable(password).Should().BeTrue();
    }

    [Fact]
    public void Every_ascii_punctuation_character_counts_as_special()
    {
        const string punctuation = "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        punctuation.ToCharArray().Should().OnlyContain(c => PasswordRules.IsSpecial(c));
        "aZ09 \t\u00e9\u00a3".ToCharArray().Should().NotContain(c => PasswordRules.IsSpecial(c));
    }

    [Fact]
    public void The_message_states_each_requirement()
    {
        PasswordRules.Message.Should().Contain(PasswordRules.MinimumLength.ToString())
            .And.Contain("number").And.Contain("special character");
    }

    [Fact]
    public void Generated_passwords_always_meet_the_rule()
    {
        var generated = Enumerable.Range(0, 2000).Select(_ => PasswordRules.Generate()).ToList();

        generated.Should().OnlyContain(p => PasswordRules.IsAcceptable(p));
        generated.Distinct().Should().HaveCount(generated.Count);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("short1!", false)]
    [InlineData("correcthorsebattery", false)]
    [InlineData("correct-horse-battery-9", true)]
    public void The_attribute_applies_the_rule_and_leaves_an_absent_password_to_required(string? password, bool valid)
    {
        new AcceptablePasswordAttribute().IsValid(password).Should().Be(valid);
    }

    [Fact]
    public void Ensure_names_the_property()
    {
        var act = () => PasswordRules.Ensure("short", "NewPassword");

        act.Should().Throw<ValidationException>()
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == "NewPassword" && e.ErrorMessage == PasswordRules.Message);
    }

    [Fact]
    public void The_request_validators_apply_the_same_rule()
    {
        new ResetPasswordRequestValidator()
            .Validate(new ResetPasswordRequest { Token = "t", NewPassword = "short", ConfirmPassword = "short" })
            .Errors.Should().Contain(e => e.ErrorMessage == PasswordRules.Message);

        new AcceptInvitationRequestValidator()
            .Validate(new AcceptInvitationRequest { Token = "t", Password = "short", ConfirmPassword = "short" })
            .Errors.Should().Contain(e => e.ErrorMessage == PasswordRules.Message);
    }

    [Fact]
    public async Task Changing_your_own_password_to_a_short_one_is_refused()
    {
        await using var db = NewDb();
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), Email = "ann@example.com", Username = "ann@example.com",
            FirstName = "Ann", LastName = "Example", PasswordHash = "old-hash", IsActive = true,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.VerifyPassword(It.IsAny<string>(), It.IsAny<string>())).Returns(true);
        var userLock = new Mock<IUserAdvisoryLockService>();
        userLock.Setup(l => l.AcquireAsync(It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());
        var service = new UserProfileService(
            db, hasher.Object, Mock.Of<IFileUrlService>(), Mock.Of<IJwtMinIatService>(), userLock.Object,
            NullLogger<UserProfileService>.Instance);

        var change = async () => await service.ChangePasswordAsync(user.Id, new ChangePasswordRequest
        {
            CurrentPassword = "current-password", NewPassword = "a", ConfirmPassword = "a",
        });

        (await change.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.PropertyName == nameof(ChangePasswordRequest.NewPassword));
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
        (await db.Users.AsNoTracking().SingleAsync()).PasswordHash.Should().Be("old-hash");
    }

    [Fact]
    public async Task First_run_registration_with_a_short_password_is_refused()
    {
        await using var db = NewDb();
        var hasher = new Mock<IPasswordHasher>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["SelfHosted:TenantId"] = Guid.NewGuid().ToString() })
            .Build();
        var service = new AuthenticationService(
            db, hasher.Object, Mock.Of<ITokenService>(), config, Mock.Of<IContactService>(),
            Mock.Of<IJwtMinIatService>(), Mock.Of<IUserAdvisoryLockService>(), new MemoryLoginThrottle(),
            NullLogger<AuthenticationService>.Instance);

        var register = async () => await service.RegisterAsync(new RegisterRequest
        {
            Email = "operator@example.com", Password = "short", ConfirmPassword = "short",
            FirstName = "Operator", LastName = "Example",
        }, "127.0.0.1", "tests", autoLogin: false);

        await register.Should().ThrowAsync<ValidationException>();
        hasher.Verify(h => h.HashPassword(It.IsAny<string>()), Times.Never);
        (await db.Users.IgnoreQueryFilters().AnyAsync()).Should().BeFalse();
    }

    private static HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"password-rules-{Guid.NewGuid()}")
            .Options);
}
