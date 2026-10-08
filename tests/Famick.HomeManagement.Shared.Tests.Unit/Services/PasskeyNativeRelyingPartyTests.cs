using System.Text.Json;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
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
/// The native mobile app runs its passkey ceremonies against a second relying party.
/// <para>
/// A native app can only drive a ceremony for a relying party it is statically associated with — an
/// iOS <c>webcredentials:</c> entitlement, or an Android <c>get_login_creds</c> entry in that
/// domain's <c>assetlinks.json</c>. Both are fixed at build time, so a household's own hostname can
/// never be one. The resolution is that the RP ID need not be the API host: a self-hosted or proxied
/// server can verify an assertion whose RP ID is <c>app.famick.com</c>, because the OS validates the
/// association against that domain while the server only checks the signature against the key it
/// stored.
/// </para>
/// <para>
/// These tests pin the two things that make that safe to operate: options are built with the
/// configuration the client asked for, and a stored credential is only ever offered to the
/// configuration it was registered under.
/// </para>
/// </summary>
public class PasskeyNativeRelyingPartyTests
{
    private const string WebRp = "home.example.com";
    private const string NativeRp = "app.famick.com";
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"passkey-native-{Guid.NewGuid()}")
            .Options);

    /// <summary>
    /// A self-hosted-shaped deployment: the web relying party is the household's own hostname, and
    /// the native one is the Famick domain the app is associated with. This is the configuration
    /// where the two differ and the filtering actually does something.
    /// </summary>
    private static ExternalAuthSettings SplitRelyingParties() => new()
    {
        Passkey = new PasskeySettings
        {
            Enabled = true,
            RelyingPartyId = WebRp,
            RelyingPartyName = "Famick Home Management",
            Origins = [$"https://{WebRp}"],
            NativeRelyingPartyId = NativeRp,
            NativeOrigins = [$"https://{NativeRp}", "android:apk-key-hash:TESTHASH"],
        }
    };

    private static PasskeyService BuildService(
        HomeManagementDbContext db,
        ExternalAuthSettings settings,
        IMemoryCache? cache = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SelfHosted:TenantId"] = TenantId.ToString(),
            })
            .Build();

        // The injected IFido2 is the *web* relying party, exactly as InfrastructureStartup wires it.
        // The native instance is built by the service itself, which is what these tests exercise.
        var webFido2 = new Fido2(new Fido2Configuration
        {
            ServerDomain = settings.Passkey.RelyingPartyId,
            ServerName = settings.Passkey.RelyingPartyName,
            Origins = settings.Passkey.Origins.ToHashSet(),
        });

        return new PasskeyService(
            context: db,
            tokenService: Mock.Of<ITokenService>(),
            configuration: config,
            contactService: Mock.Of<IContactService>(),
            setupService: new SetupService(db, NullLogger<SetupService>.Instance),
            cache: cache ?? new MemoryCache(new MemoryCacheOptions()),
            fido2: webFido2,
            settings: Options.Create(settings),
            logger: NullLogger<PasskeyService>.Instance);
    }

    private static User SeedUser(HomeManagementDbContext db, params UserPasskeyCredential[] credentials)
    {
        var user = new User
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
        };
        db.Users.Add(user);

        foreach (var c in credentials)
        {
            c.UserId = user.Id;
            c.TenantId = TenantId;
            db.UserPasskeyCredentials.Add(c);
        }

        db.SaveChanges();
        return user;
    }

    private static UserPasskeyCredential Credential(string label, string? relyingPartyId) => new()
    {
        Id = Guid.NewGuid(),
        CredentialId = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(label.PadRight(16))),
        PublicKey = Convert.ToBase64String([1, 2, 3]),
        SignatureCounter = 0,
        DeviceName = label,
        RelyingPartyId = relyingPartyId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static string RpIdOf(string optionsJson) =>
        JsonDocument.Parse(optionsJson).RootElement.TryGetProperty("rp", out var rp)
            ? rp.GetProperty("id").GetString()!
            : JsonDocument.Parse(optionsJson).RootElement.GetProperty("rpId").GetString()!;

    private static List<string> AllowedCredentialIds(string optionsJson)
    {
        var root = JsonDocument.Parse(optionsJson).RootElement;
        var key = root.TryGetProperty("allowCredentials", out _) ? "allowCredentials" : "excludeCredentials";
        return root.TryGetProperty(key, out var arr) && arr.ValueKind == JsonValueKind.Array
            ? arr.EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList()
            : [];
    }

    [Theory]
    [InlineData(null, WebRp)]                        // absent -> web, so older clients are unaffected
    [InlineData("", WebRp)]
    [InlineData("web", WebRp)]
    [InlineData("browser", WebRp)]                   // unrecognised -> web, never native by accident
    [InlineData("native", NativeRp)]
    [InlineData("NATIVE", NativeRp)]                 // case-insensitive
    public async Task RegisterOptions_UseTheRelyingPartyTheClientAsksFor(string? clientType, string expectedRp)
    {
        using var db = NewDb();
        var user = SeedUser(db);
        var service = BuildService(db, SplitRelyingParties());

        var response = await service.GetRegisterOptionsAsync(
            user.Id,
            new PasskeyRegisterOptionsRequest { ClientType = clientType },
            CancellationToken.None);

        RpIdOf(response.Options).Should().Be(expectedRp);
    }

    [Theory]
    [InlineData(null, WebRp)]
    [InlineData("native", NativeRp)]
    public async Task AuthenticateOptions_UseTheRelyingPartyTheClientAsksFor(string? clientType, string expectedRp)
    {
        using var db = NewDb();
        SeedUser(db);
        var service = BuildService(db, SplitRelyingParties());

        var response = await service.GetAuthenticateOptionsAsync(
            new PasskeyAuthenticateOptionsRequest { ClientType = clientType },
            CancellationToken.None);

        RpIdOf(response.Options).Should().Be(expectedRp);
    }

    [Fact]
    public async Task AuthenticateOptions_OfferOnlyCredentialsTheCeremonyCanVerify()
    {
        // A credential is bound to one RP ID, so offering the browser's to the app would put a
        // passkey in the OS sheet that cannot verify — the user picks it and gets an opaque failure.
        using var db = NewDb();
        var webCred = Credential("browser-passkey", WebRp);
        var nativeCred = Credential("phone-passkey", NativeRp);
        var user = SeedUser(db, webCred, nativeCred);
        var service = BuildService(db, SplitRelyingParties());

        var web = await service.GetAuthenticateOptionsAsync(
            new PasskeyAuthenticateOptionsRequest { Email = user.Email },
            CancellationToken.None);
        var native = await service.GetAuthenticateOptionsAsync(
            new PasskeyAuthenticateOptionsRequest { Email = user.Email, ClientType = "native" },
            CancellationToken.None);

        AllowedCredentialIds(web.Options).Should().ContainSingle()
            .Which.Should().Be(ToBase64Url(webCred.CredentialId));
        AllowedCredentialIds(native.Options).Should().ContainSingle()
            .Which.Should().Be(ToBase64Url(nativeCred.CredentialId));
    }

    [Fact]
    public async Task AuthenticateOptions_TreatACredentialWithNoRecordedRelyingPartyAsWeb()
    {
        // Rows predating the RelyingPartyId column necessarily came from a browser. They must keep
        // working on the web path and must not be offered to the app, which could not verify them.
        using var db = NewDb();
        var legacy = Credential("pre-column-passkey", relyingPartyId: null);
        var user = SeedUser(db, legacy);
        var service = BuildService(db, SplitRelyingParties());

        var web = await service.GetAuthenticateOptionsAsync(
            new PasskeyAuthenticateOptionsRequest { Email = user.Email },
            CancellationToken.None);
        var native = await service.GetAuthenticateOptionsAsync(
            new PasskeyAuthenticateOptionsRequest { Email = user.Email, ClientType = "native" },
            CancellationToken.None);

        AllowedCredentialIds(web.Options).Should().ContainSingle()
            .Which.Should().Be(ToBase64Url(legacy.CredentialId));
        AllowedCredentialIds(native.Options).Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterOptions_ExcludeOnlyCredentialsFromTheSameRelyingParty()
    {
        // excludeCredentials stops re-registering a passkey the user already has. Excluding the
        // other relying party's credential would instead block them from registering a usable one.
        using var db = NewDb();
        var webCred = Credential("browser-passkey", WebRp);
        var user = SeedUser(db, webCred);
        var service = BuildService(db, SplitRelyingParties());

        var native = await service.GetRegisterOptionsAsync(
            user.Id,
            new PasskeyRegisterOptionsRequest { ClientType = "native" },
            CancellationToken.None);

        AllowedCredentialIds(native.Options).Should().BeEmpty();
    }

    [Fact]
    public async Task OnTheCloudApp_BothRelyingPartiesAreTheSameAndNothingIsFiltered()
    {
        // The cloud app's web RP ID already is app.famick.com, so web and native differ only in
        // allowed origins. Nothing should be filtered there — this guards against the split-RP
        // filtering quietly hiding cloud users' passkeys.
        using var db = NewDb();
        var settings = SplitRelyingParties();
        settings.Passkey.RelyingPartyId = NativeRp;
        settings.Passkey.Origins = [$"https://{NativeRp}"];

        var cred = Credential("cloud-passkey", NativeRp);
        var legacy = Credential("pre-column-passkey", relyingPartyId: null);
        var user = SeedUser(db, cred, legacy);
        var service = BuildService(db, settings);

        foreach (var clientType in new string?[] { null, "native" })
        {
            var options = await service.GetAuthenticateOptionsAsync(
                new PasskeyAuthenticateOptionsRequest { Email = user.Email, ClientType = clientType },
                CancellationToken.None);

            RpIdOf(options.Options).Should().Be(NativeRp);
            AllowedCredentialIds(options.Options).Should().HaveCount(2,
                "both relying parties are the same string, so no credential is out of scope");
        }
    }

    [Fact]
    public void NativeSupport_IsReportedOffOnlyWhenItIsActuallyUnconfigured()
    {
        // What the mobile app gates its UI on. Defaults are populated, so the common case is true;
        // these are the ways an operator can turn it off.
        using var db = NewDb();

        BuildService(db, SplitRelyingParties()).IsNativeEnabled.Should().BeTrue();

        var noRp = SplitRelyingParties();
        noRp.Passkey.NativeRelyingPartyId = "";
        BuildService(db, noRp).IsNativeEnabled.Should().BeFalse();

        var noOrigins = SplitRelyingParties();
        noOrigins.Passkey.NativeOrigins = [];
        BuildService(db, noOrigins).IsNativeEnabled.Should().BeFalse();

        // Passkeys off entirely implies native off, rather than the two disagreeing.
        var off = SplitRelyingParties();
        off.Passkey.Enabled = false;
        BuildService(db, off).IsNativeEnabled.Should().BeFalse();
    }

    [Fact]
    public void DefaultNativeSettings_CarryTheAppsRealIdentifiers()
    {
        // These are the app's own identifiers and identical on every deployment, which is why they
        // are code defaults rather than per-host configuration. The Android values are the base64url
        // SHA-256 of the two signing certificates in app.famick.com's assetlinks.json; if those are
        // ever rotated, this test is the reminder that the server list must change too.
        var settings = new PasskeySettings();

        settings.NativeRelyingPartyId.Should().Be("app.famick.com");
        settings.NativeOrigins.Should().BeEquivalentTo(
        [
            "https://app.famick.com",
            "android:apk-key-hash:Zj1eMSz5GLC69HrtOnIBtqAZadSisybrydjDYFmBbYs",
            "android:apk-key-hash:ef35kpfyEEotVx5E-ywC_0ZGpnHgyVEu0NJwSw1le54",
        ]);
    }

    private static string ToBase64Url(string base64) =>
        base64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
