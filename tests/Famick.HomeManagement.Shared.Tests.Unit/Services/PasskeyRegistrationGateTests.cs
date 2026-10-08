using System.Text.Json;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Fido2NetLib;
using Fido2NetLib.Objects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Self-service passkey sign-up is refused on a server that already has users.
/// <para>
/// <c>POST /api/auth/passkey/register/{options,verify}</c> is <c>[AllowAnonymous]</c>, because
/// standing up a brand-new self-hosted server with a passkey instead of a password is a legitimate
/// first-run path. What it lacked was the gate the password path has carried all along in
/// <c>AuthApiController.Register</c> — so on a server already in use, anyone able to reach an origin
/// matching the relying party could create an account: no email verification, no terms consent, no
/// household provisioning. On a multi-tenant host the rows were additionally stamped with the
/// single-tenant fallback tenant id, landing the account in a household that was not the caller's.
/// </para>
/// <para>
/// The gate reads <see cref="ISetupService.HasUsersAsync"/>, which queries with
/// <c>IgnoreQueryFilters()</c> — the point being that its answer does not depend on tenant
/// resolution, which is exactly what an unauthenticated request cannot supply. These tests use the
/// real <see cref="SetupService"/> rather than a mock so that property is exercised, not assumed.
/// </para>
/// </summary>
public class PasskeyRegistrationGateTests
{
    private static readonly Guid ConfiguredTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"passkey-gate-{Guid.NewGuid()}")
            .Options);

    private static PasskeyService BuildService(
        HomeManagementDbContext db,
        IFido2? fido2 = null,
        IMemoryCache? cache = null,
        string? selfHostedTenantId = null)
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
                ["SelfHosted:TenantId"] = selfHostedTenantId ?? ConfiguredTenantId.ToString(),
            })
            .Build();

        return new PasskeyService(
            context: db,
            tokenService: Mock.Of<ITokenService>(),
            configuration: config,
            contactService: Mock.Of<IContactService>(),
            setupService: new SetupService(db, NullLogger<SetupService>.Instance),
            cache: cache ?? new MemoryCache(new MemoryCacheOptions()),
            fido2: fido2 ?? RealFido2(),
            settings: Options.Create(settings),
            logger: NullLogger<PasskeyService>.Instance);
    }

    private static IFido2 RealFido2() => new Fido2(new Fido2Configuration
    {
        ServerDomain = "localhost",
        ServerName = "HomeManagement",
        Origins = new HashSet<string> { "https://localhost" },
    });

    private static User SeedUser(HomeManagementDbContext db, Guid tenantId)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = "resident@example.com",
            Username = "resident@example.com",
            FirstName = "Resident",
            LastName = "Example",
            PasswordHash = "not-a-real-hash",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task GetRegisterOptions_Anonymously_OnAServerWithUsers_IsRefused()
    {
        using var db = NewDb();
        SeedUser(db, ConfiguredTenantId);
        var service = BuildService(db);

        var act = async () => await service.GetRegisterOptionsAsync(
            userId: null,
            new PasskeyRegisterOptionsRequest
            {
                Email = "intruder@example.com",
                FirstName = "In",
                LastName = "Truder",
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<RegistrationClosedException>();
    }

    [Fact]
    public async Task GetRegisterOptions_Anonymously_DoesNotRevealWhetherAnEmailExists()
    {
        // The duplicate-email check raises DuplicateEntityException -> 409, and it runs unfiltered,
        // so before the gate an unauthenticated caller could use it to test whether any household
        // on the server held a given address. The gate has to come first for that reason, not just
        // to stop the account being created — hence a test for the distinction between the two
        // failures rather than only for "it threw".
        using var db = NewDb();
        var existing = SeedUser(db, ConfiguredTenantId);
        var service = BuildService(db);

        var knownAddress = async () => await service.GetRegisterOptionsAsync(
            userId: null,
            new PasskeyRegisterOptionsRequest { Email = existing.Email },
            CancellationToken.None);

        var unknownAddress = async () => await service.GetRegisterOptionsAsync(
            userId: null,
            new PasskeyRegisterOptionsRequest { Email = "nobody@example.com" },
            CancellationToken.None);

        (await knownAddress.Should().ThrowAsync<RegistrationClosedException>()).Which.Message
            .Should().Be((await unknownAddress.Should().ThrowAsync<RegistrationClosedException>())
                .Which.Message);
    }

    [Fact]
    public async Task VerifyRegister_Anonymously_OnAServerWithUsers_IsRefused()
    {
        // Options and verify are separate requests, so nothing carries the earlier refusal forward,
        // and a session minted before this gate deployed stays in the cache for five minutes. Verify
        // has to re-check rather than trust that options said no.
        using var db = NewDb();
        SeedUser(db, ConfiguredTenantId);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var fido2 = RealFido2();
        var service = BuildService(db, fido2, cache);

        // Mint a real anonymous session the way GetRegisterOptionsAsync would have, on a server that
        // was still empty at the time.
        using var emptyDb = NewDb();
        var sessionId = (await BuildService(emptyDb, fido2, cache).GetRegisterOptionsAsync(
            userId: null,
            new PasskeyRegisterOptionsRequest { Email = "intruder@example.com" },
            CancellationToken.None)).SessionId;

        var act = async () => await service.VerifyRegisterAsync(
            userId: null,
            new PasskeyRegisterVerifyRequest
            {
                SessionId = sessionId,
                AttestationResponse = EmptyAttestationJson,
            },
            ipAddress: "127.0.0.1",
            deviceInfo: "tests",
            CancellationToken.None);

        await act.Should().ThrowAsync<RegistrationClosedException>();
        db.UserPasskeyCredentials.IgnoreQueryFilters().Should().BeEmpty();
    }

    [Fact]
    public async Task GetRegisterOptions_Anonymously_OnAnEmptyServer_IsAllowed()
    {
        // The first-run case the anonymous path exists for: a fresh self-hosted server being set up
        // with a passkey instead of a password. The gate must not close this.
        using var db = NewDb();
        var service = BuildService(db);

        var response = await service.GetRegisterOptionsAsync(
            userId: null,
            new PasskeyRegisterOptionsRequest
            {
                Email = "owner@example.com",
                FirstName = "Owner",
                LastName = "Example",
            },
            CancellationToken.None);

        response.SessionId.Should().NotBeNullOrWhiteSpace();
        response.Options.Should().Contain("\"challenge\"");
    }

    [Fact]
    public async Task GetRegisterOptions_ForASignedInUser_IsAllowedOnAServerWithUsers()
    {
        // Adding a passkey to an existing account is the authenticated path and is unaffected by the
        // gate — it is the whole point of the feature.
        using var db = NewDb();
        var user = SeedUser(db, ConfiguredTenantId);
        var service = BuildService(db);

        var response = await service.GetRegisterOptionsAsync(
            user.Id,
            new PasskeyRegisterOptionsRequest { DeviceName = "Their phone" },
            CancellationToken.None);

        response.SessionId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task VerifyRegister_ForASignedInUser_StoresTheCredentialInTheUsersOwnTenant()
    {
        // The tenant used to come from SelfHosted:TenantId for both branches. That key is unset on a
        // multi-tenant host, so the fallback won and a cloud user's passkey was stamped with the
        // single-tenant id rather than their household. Here the configured value is deliberately
        // *not* the user's tenant, so reading configuration would fail this test.
        var householdTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        using var db = NewDb();
        var user = SeedUser(db, householdTenantId);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var credentialId = new byte[] { 1, 2, 3, 4 };

        var fido2 = new Mock<IFido2>();
        fido2.Setup(f => f.RequestNewCredential(It.IsAny<RequestNewCredentialParams>()))
            .Returns(RealFido2().RequestNewCredential(new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = user.Id.ToByteArray(),
                    Name = user.Email,
                    DisplayName = user.Email,
                },
                ExcludeCredentials = [],
            }));
        fido2.Setup(f => f.MakeNewCredentialAsync(
                It.IsAny<MakeNewCredentialParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisteredPublicKeyCredential
            {
                Id = credentialId,
                PublicKey = [9, 9, 9],
                SignCount = 0,
                Type = PublicKeyCredentialType.PublicKey,
                AaGuid = Guid.Empty,
            });

        var service = BuildService(db, fido2.Object, cache, selfHostedTenantId: ConfiguredTenantId.ToString());

        var options = await service.GetRegisterOptionsAsync(
            user.Id,
            new PasskeyRegisterOptionsRequest { DeviceName = "Their phone" },
            CancellationToken.None);

        var result = await service.VerifyRegisterAsync(
            user.Id,
            new PasskeyRegisterVerifyRequest
            {
                SessionId = options.SessionId,
                AttestationResponse = EmptyAttestationJson,
                DeviceName = "Their phone",
            },
            ipAddress: "127.0.0.1",
            deviceInfo: "tests",
            CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);

        var stored = await db.UserPasskeyCredentials.IgnoreQueryFilters().SingleAsync();
        stored.TenantId.Should().Be(householdTenantId);
        stored.TenantId.Should().NotBe(ConfiguredTenantId);
        stored.UserId.Should().Be(user.Id);
    }

    [Fact]
    public async Task VerifyRegister_Anonymously_WithoutAConfiguredTenant_FailsLoudly()
    {
        // Previously this fell back to 00000000-0000-0000-0000-000000000001, writing the household
        // into a tenant nothing else resolves to: the account is created, the user signs in, and the
        // app is empty forever. Failing is the better answer.
        using var db = NewDb();

        var cache = new MemoryCache(new MemoryCacheOptions());
        var fido2 = new Mock<IFido2>();
        fido2.Setup(f => f.RequestNewCredential(It.IsAny<RequestNewCredentialParams>()))
            .Returns(RealFido2().RequestNewCredential(new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = Guid.NewGuid().ToByteArray(),
                    Name = "owner@example.com",
                    DisplayName = "Owner",
                },
                ExcludeCredentials = [],
            }));
        fido2.Setup(f => f.MakeNewCredentialAsync(
                It.IsAny<MakeNewCredentialParams>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RegisteredPublicKeyCredential
            {
                Id = [1, 2, 3, 4],
                PublicKey = [9, 9, 9],
                SignCount = 0,
                Type = PublicKeyCredentialType.PublicKey,
                AaGuid = Guid.Empty,
            });

        var service = BuildService(db, fido2.Object, cache, selfHostedTenantId: "");

        var options = await service.GetRegisterOptionsAsync(
            userId: null,
            new PasskeyRegisterOptionsRequest { Email = "owner@example.com" },
            CancellationToken.None);

        var result = await service.VerifyRegisterAsync(
            userId: null,
            new PasskeyRegisterVerifyRequest
            {
                SessionId = options.SessionId,
                AttestationResponse = EmptyAttestationJson,
            },
            ipAddress: "127.0.0.1",
            deviceInfo: "tests",
            CancellationToken.None);

        // VerifyRegisterAsync converts in-ceremony failures into a failed response rather than
        // throwing, so the assertion is on the outcome: nothing was written.
        result.Success.Should().BeFalse();
        db.Users.IgnoreQueryFilters().Should().BeEmpty();
        db.UserPasskeyCredentials.IgnoreQueryFilters().Should().BeEmpty();
    }

    /// <summary>
    /// Deserializes into a non-null <c>AuthenticatorAttestationRawResponse</c> so the method reaches
    /// the mocked <c>MakeNewCredentialAsync</c>. The bytes are never inspected — verification itself
    /// is Fido2NetLib's job and is covered by its own suite.
    /// </summary>
    private static readonly string EmptyAttestationJson = JsonSerializer.Serialize(new
    {
        id = "AQIDBA",
        rawId = "AQIDBA",
        type = "public-key",
        response = new
        {
            attestationObject = "AQ",
            clientDataJSON = "AQ",
        },
    });
}
