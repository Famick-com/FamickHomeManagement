using System.Text.Json;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Fido2NetLib;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Passkey-first signup: creation options for an account that does not exist yet.
/// <para>
/// This is the one flow that has to mint WebAuthn options before there is a user, which is exactly
/// what the anonymous registration branch did before it was gated off for creating accounts with no
/// email verification, consent or household. The replacement keeps that gate shut and moves the
/// authorization to the registration token, so the tests here are mostly about the seams rather than
/// the happy path: the session must not be redeemable through the old path, and the user handle must
/// survive to become the new user's id.
/// </para>
/// </summary>
public class PasskeySignupTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"passkey-signup-{Guid.NewGuid()}")
            .Options);

    private static PasskeyService BuildService(HomeManagementDbContext db, IMemoryCache cache)
    {
        var settings = new ExternalAuthSettings
        {
            Passkey = new PasskeySettings
            {
                Enabled = true,
                RelyingPartyId = "localhost",
                RelyingPartyName = "HomeManagement",
                Origins = ["https://localhost"],
            }
        };

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SelfHosted:TenantId"] = TenantId.ToString(),
            })
            .Build();

        return new PasskeyService(
            context: db,
            tokenService: Mock.Of<ITokenService>(),
            configuration: config,
            contactService: Mock.Of<IContactService>(),
            setupService: new SetupService(db, NullLogger<SetupService>.Instance),
            cache: cache,
            fido2: new Fido2(new Fido2Configuration
            {
                ServerDomain = "localhost",
                ServerName = "HomeManagement",
                Origins = new HashSet<string> { "https://localhost" },
            }),
            settings: Options.Create(settings),
            logger: NullLogger<PasskeyService>.Instance);
    }

    private static void SeedUser(HomeManagementDbContext db)
    {
        db.Users.Add(new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Email = "resident@example.com",
            Username = "resident@example.com",
            FirstName = "Resident",
            LastName = "Example",
            PasswordHash = "not-a-real-hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Options_AreIssuedOnAServerWithUsers()
    {
        // The whole point. GetRegisterOptionsAsync's anonymous branch is refused here; this path is
        // authorized by the caller having validated a verification token, so it must still work.
        using var db = NewDb();
        SeedUser(db);
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var response = await BuildService(db, cache).CreatePendingSignupOptionsAsync(
            "newcomer@example.com", "New Comer", "Their phone", native: false, CancellationToken.None);

        response.SessionId.Should().NotBeNullOrWhiteSpace();
        response.Options.Should().Contain("\"challenge\"");
    }

    [Fact]
    public async Task Options_RequireResidentKeys()
    {
        // An account with no password whose only credential is non-discoverable could not be used
        // for the usernameless sign-in this flow exists to enable, and there would be nothing to fall
        // back on. "required", not the "preferred" the other registration path asks for.
        using var db = NewDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var response = await BuildService(db, cache).CreatePendingSignupOptionsAsync(
            "newcomer@example.com", null, null, native: false, CancellationToken.None);

        var selection = JsonDocument.Parse(response.Options).RootElement
            .GetProperty("authenticatorSelection");
        selection.GetProperty("residentKey").GetString().Should().Be("required");
        selection.GetProperty("requireResidentKey").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Options_UseTheVerifiedEmailAndAFreshServerMintedHandle()
    {
        // The handle is the new user's id, so it must come from the server. Two calls for the same
        // address must not share one, or two concurrent signups would collide on the primary key.
        using var db = NewDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildService(db, cache);

        var first = await service.CreatePendingSignupOptionsAsync(
            "Newcomer@Example.com ", null, null, native: false, CancellationToken.None);
        var second = await service.CreatePendingSignupOptionsAsync(
            "newcomer@example.com", null, null, native: false, CancellationToken.None);

        static (string Name, string Id) User(string optionsJson)
        {
            var user = JsonDocument.Parse(optionsJson).RootElement.GetProperty("user");
            return (user.GetProperty("name").GetString()!, user.GetProperty("id").GetString()!);
        }

        var a = User(first.Options);
        var b = User(second.Options);

        // Normalised, so the address the account is created for does not depend on how it was typed.
        a.Name.Should().Be("newcomer@example.com");
        b.Name.Should().Be("newcomer@example.com");
        a.Id.Should().NotBe(b.Id);
        first.SessionId.Should().NotBe(second.SessionId);
    }

    [Fact]
    public async Task APendingSignupSession_CannotBeRedeemedThroughTheGatedRegistrationPath()
    {
        // The seam that matters. If VerifyRegisterAsync could find one of these sessions, it would
        // create a user by itself — no token, no consent, no household — which is the behaviour that
        // had to be closed off. The distinct cache-key prefix is what prevents it; this test fails if
        // anyone unifies them.
        using var db = NewDb();
        SeedUser(db);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildService(db, cache);

        var options = await service.CreatePendingSignupOptionsAsync(
            "newcomer@example.com", null, null, native: false, CancellationToken.None);

        var redeemed = await service.VerifyRegisterAsync(
            userId: null,
            new PasskeyRegisterVerifyRequest
            {
                SessionId = options.SessionId,
                AttestationResponse = "{}",
            },
            ipAddress: "127.0.0.1",
            deviceInfo: "tests",
            CancellationToken.None);

        redeemed.Success.Should().BeFalse();
        redeemed.ErrorMessage.Should().Be("Session expired or invalid");
        db.Users.IgnoreQueryFilters().Should().HaveCount(1, "no user should have been created");
    }

    [Fact]
    public async Task VerifyPendingSignup_RefusesAnUnknownOrAlreadyUsedSession()
    {
        using var db = NewDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = BuildService(db, cache);

        var unknown = async () => await service.VerifyPendingSignupAsync(
            "not-a-session", "{}", CancellationToken.None);

        await unknown.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Passkey session expired or invalid");

        // Single use: the session is dropped before verification, so a failed attempt cannot be
        // retried against the same challenge.
        var options = await service.CreatePendingSignupOptionsAsync(
            "newcomer@example.com", null, null, native: false, CancellationToken.None);

        var firstAttempt = async () => await service.VerifyPendingSignupAsync(
            options.SessionId, "{}", CancellationToken.None);
        await firstAttempt.Should().ThrowAsync<Exception>();

        var replay = async () => await service.VerifyPendingSignupAsync(
            options.SessionId, "{}", CancellationToken.None);
        await replay.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Passkey session expired or invalid");
    }

    [Fact]
    public async Task Options_AreRefusedWhenPasskeysAreNotConfigured()
    {
        using var db = NewDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var settings = new ExternalAuthSettings
        {
            Passkey = new PasskeySettings { Enabled = false }
        };

        var service = new PasskeyService(
            context: db,
            tokenService: Mock.Of<ITokenService>(),
            configuration: new ConfigurationBuilder().Build(),
            contactService: Mock.Of<IContactService>(),
            setupService: new SetupService(db, NullLogger<SetupService>.Instance),
            cache: cache,
            fido2: Mock.Of<IFido2>(),
            settings: Options.Create(settings),
            logger: NullLogger<PasskeyService>.Instance);

        var act = async () => await service.CreatePendingSignupOptionsAsync(
            "newcomer@example.com", null, null, native: false, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Options_AreRefusedWithoutAnEmail()
    {
        // The email comes from the verification token, so a blank one means the caller wired
        // something wrongly rather than that the user left a field empty. Fail rather than create a
        // credential for an account that could never be addressed.
        using var db = NewDb();
        using var cache = new MemoryCache(new MemoryCacheOptions());

        var act = async () => await BuildService(db, cache).CreatePendingSignupOptionsAsync(
            "  ", null, null, native: false, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
