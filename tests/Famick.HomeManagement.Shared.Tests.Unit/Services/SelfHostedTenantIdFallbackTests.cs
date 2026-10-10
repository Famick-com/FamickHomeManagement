using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.DTOs.Users;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using Famick.HomeManagement.Messaging.Interfaces;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// The three services that used to resolve a tenant by falling back to the single-tenant id
/// <c>00000000-0000-0000-0000-000000000001</c> when <c>SelfHosted:TenantId</c> (or the request's
/// tenant) was absent.
/// <para>
/// On a multi-tenant host that key is unset by design, so the fallback always won and the rows
/// those services wrote were stamped with a household that was not the caller's. The reason it
/// never looked broken is the shape of the global query filter —
/// <c>CurrentTenantId == null || e.TenantId == CurrentTenantId</c> — which matches every row on a
/// request whose tenant is unresolved, exactly the kind of request these paths run on. The row
/// only disappears later, on a request that does resolve a tenant. That is the failure that made
/// a mismatched passkey credential vanish from Settings -> Profile and 404 on rename, and these
/// tests are the executable version of "do not re-introduce it".
/// </para>
/// <para>
/// Two different fixes are asserted here, because the two situations are not the same. Where a
/// correct tenant is already in hand — the user being linked to — it is used
/// (<see cref="ExternalAuthService"/>'s link branch). Where the only source is configuration or
/// request state, absence now throws rather than defaulting: a wrong-but-plausible tenant writes
/// a household into a tenant nothing else resolves to, which reads as an account that is empty
/// forever, and that is worse than a 500.
/// </para>
/// </summary>
public class SelfHostedTenantIdFallbackTests
{
    /// <summary>The id the three call sites used to fall back to.</summary>
    private static readonly Guid FallbackTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static readonly Guid HouseholdTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ConfiguredTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    // ---------------------------------------------------------------------
    // Site 1 — ExternalAuthService.FindOrCreateUserAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task ProcessCallback_LinkingToAnExistingUser_StampsTheUsersOwnTenant_NotTheConfiguredOne()
    {
        // The divergent-config case: a value is present but belongs to a different household.
        // Before the fix the link row took the configured id; the user's own tenant is the only
        // correct answer, and it is available right there in existingUser.
        var dbName = NewDbName();
        var user = SeedUserInto(dbName, HouseholdTenantId, "resident@example.com");

        using var db = NewDb(dbName);
        var sut = BuildExternalAuthService(db, selfHostedTenantId: ConfiguredTenantId.ToString());

        await ProcessOidcCallbackAsync(sut, "resident@example.com");

        var link = await db.UserExternalLogins.IgnoreQueryFilters().SingleAsync();
        link.UserId.Should().Be(user.Id);
        link.TenantId.Should().Be(HouseholdTenantId);
        link.TenantId.Should().NotBe(ConfiguredTenantId);
    }

    [Fact]
    public async Task ProcessCallback_LinkingToAnExistingUser_OnAMultiTenantHost_StampsTheUsersTenant()
    {
        // The actual cloud case: SelfHosted:TenantId is not configured at all. Previously the
        // null-coalesce handed back the single-tenant fallback id and every user_external_logins
        // row on the cloud database was written against it.
        var dbName = NewDbName();
        SeedUserInto(dbName, HouseholdTenantId, "resident@example.com");

        using var db = NewDb(dbName);
        var sut = BuildExternalAuthService(db, selfHostedTenantId: null);

        await ProcessOidcCallbackAsync(sut, "resident@example.com");

        var link = await db.UserExternalLogins.IgnoreQueryFilters().SingleAsync();
        link.TenantId.Should().Be(HouseholdTenantId);
        link.TenantId.Should().NotBe(FallbackTenantId);
    }

    [Fact]
    public async Task ProcessCallback_TheLinkRowIsVisibleOnceTheTenantResolves()
    {
        // The point of the whole exercise, stated as the symptom rather than the cause. A row
        // stamped with the wrong tenant still reads back on an unresolved request — which is why
        // this never showed up in testing — and only vanishes on a request that resolves one.
        // So assert through a context that *does* resolve the household's tenant.
        var dbName = NewDbName();
        SeedUserInto(dbName, HouseholdTenantId, "resident@example.com");

        using (var writeDb = NewDb(dbName))
        {
            var sut = BuildExternalAuthService(writeDb, selfHostedTenantId: null);
            await ProcessOidcCallbackAsync(sut, "resident@example.com");
        }

        using var scopedDb = NewDb(dbName, tenantId: HouseholdTenantId);
        (await scopedDb.UserExternalLogins.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ProcessCallback_CreatingTheFirstUser_UsesTheConfiguredTenantForEveryRow()
    {
        // The first-run branch is the one legitimate reader of SelfHosted:TenantId — it is gated
        // on there being no users at all, which is never true on a populated cloud database.
        var dbName = NewDbName();
        using var db = NewDb(dbName);
        var sut = BuildExternalAuthService(db, selfHostedTenantId: ConfiguredTenantId.ToString());

        await ProcessOidcCallbackAsync(sut, "operator@example.com");

        var created = await db.Users.IgnoreQueryFilters().SingleAsync();
        created.TenantId.Should().Be(ConfiguredTenantId);
        (await db.UserRoles.IgnoreQueryFilters().SingleAsync()).TenantId.Should().Be(ConfiguredTenantId);
        (await db.UserExternalLogins.IgnoreQueryFilters().SingleAsync()).TenantId.Should().Be(ConfiguredTenantId);
    }

    [Fact]
    public async Task ProcessCallback_CreatingTheFirstUser_WithoutSelfHostedTenantId_IsRefused()
    {
        using var db = NewDb(NewDbName());
        var sut = BuildExternalAuthService(db, selfHostedTenantId: null);

        var act = async () => await ProcessOidcCallbackAsync(sut, "operator@example.com");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SelfHosted:TenantId is not configured*");
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // Site 2 — AuthenticationService.RegisterAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Register_UsesTheConfiguredTenant()
    {
        using var db = NewDb(NewDbName());
        var sut = BuildAuthenticationService(db, selfHostedTenantId: ConfiguredTenantId.ToString());

        await sut.RegisterAsync(NewRegisterRequest(), "127.0.0.1", "tests", autoLogin: false);

        var created = await db.Users.IgnoreQueryFilters().SingleAsync();
        created.TenantId.Should().Be(ConfiguredTenantId);
        (await db.UserRoles.IgnoreQueryFilters().SingleAsync()).TenantId.Should().Be(ConfiguredTenantId);
    }

    [Fact]
    public async Task Register_WithoutSelfHostedTenantId_IsRefused()
    {
        // Reachable only at first run — AuthApiController.Register refuses once
        // ISetupService.HasUsersAsync is true — so this is a configuration assertion, not a
        // request-time one. Every shipped deployment asset sets the key; the Home Assistant
        // add-on generates a random per-install UUID for it, which is why defaulting to the
        // single-tenant id was wrong there too and not only on cloud.
        using var db = NewDb(NewDbName());
        var sut = BuildAuthenticationService(db, selfHostedTenantId: null);

        var act = async () => await sut.RegisterAsync(
            NewRegisterRequest(), "127.0.0.1", "tests", autoLogin: false);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SelfHosted:TenantId is not configured*");
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Register_WithAnUnparseableSelfHostedTenantId_IsRefused()
    {
        // The old code read the key then called Guid.Parse on it, so a typo threw FormatException
        // from inside the registration flow. Absent and malformed are the same failure to an
        // operator, so they should read the same way.
        using var db = NewDb(NewDbName());
        var sut = BuildAuthenticationService(db, selfHostedTenantId: "not-a-uuid");

        var act = async () => await sut.RegisterAsync(
            NewRegisterRequest(), "127.0.0.1", "tests", autoLogin: false);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*SelfHosted:TenantId is not configured*");
    }

    // ---------------------------------------------------------------------
    // Site 3 — UserManagementService.CreateUserAsync
    // ---------------------------------------------------------------------

    [Fact]
    public async Task CreateUser_StampsTheResolvedTenantOnTheUserAndEveryRole()
    {
        using var db = NewDb(NewDbName());
        var sut = BuildUserManagementService(db, resolvedTenantId: HouseholdTenantId);

        await sut.CreateUserAsync(NewCreateUserRequest(), "https://app.famick.com");

        (await db.Users.IgnoreQueryFilters().SingleAsync()).TenantId.Should().Be(HouseholdTenantId);
        var roles = await db.UserRoles.IgnoreQueryFilters().ToListAsync();
        roles.Should().HaveCount(2);
        roles.Should().OnlyContain(r => r.TenantId == HouseholdTenantId);
    }

    [Fact]
    public async Task CreateUser_WithNoResolvedTenant_IsRefusedRatherThanDefaulted()
    {
        // Not reachable on cloud today: UsersController is [Authorize(Policy = "RequireAdmin")],
        // the app registers one JWT issuer, and TokenService writes tenant_id unconditionally, so
        // HttpContextTenantProvider always has an answer. The safety therefore lives entirely in
        // other files. One tenant value here stamps the users row, every user_roles row, and
        // scopes the contacts lookup, so defaulting would file a whole member into another
        // household and quietly fail to link the contact the admin picked.
        using var db = NewDb(NewDbName());
        var sut = BuildUserManagementService(db, resolvedTenantId: null);

        var act = async () => await sut.CreateUserAsync(NewCreateUserRequest(), "https://app.famick.com");

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*No tenant is resolved for this request*");
        (await db.Users.IgnoreQueryFilters().CountAsync()).Should().Be(0);
    }

    // ---------------------------------------------------------------------
    // Harness
    // ---------------------------------------------------------------------

    private static string NewDbName() => $"tenant-fallback-{Guid.NewGuid()}";

    /// <summary>
    /// A context over the named in-memory store. <paramref name="tenantId"/> left null gives the
    /// unresolved-tenant request these auth paths actually run on, where the global filter matches
    /// every row; passing one gives the resolved request where a mis-stamped row disappears.
    /// </summary>
    private static HomeManagementDbContext NewDb(string dbName, Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        if (tenantId is null)
        {
            return new HomeManagementDbContext(options);
        }

        var provider = new Mock<ITenantProvider>();
        provider.Setup(p => p.TenantId).Returns(tenantId);
        return new HomeManagementDbContext(
            options,
            provider.Object,
            new MultiTenancyOptions { IsMultiTenantEnabled = true, FixedTenantId = null });
    }

    private static User SeedUserInto(string dbName, Guid tenantId, string email)
    {
        using var db = NewDb(dbName);

        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test Household", CreatedAt = DateTime.UtcNow });

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Username = email,
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

    private static IConfiguration Config(string? selfHostedTenantId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SelfHosted:TenantId"] = selfHostedTenantId,
            })
            .Build();

    // --- Site 1 harness ---

    private static ExternalAuthService BuildExternalAuthService(
        HomeManagementDbContext db,
        string? selfHostedTenantId)
    {
        var settings = new ExternalAuthSettings
        {
            OpenIdConnect = new OidcAuthSettings
            {
                Enabled = true,
                Authority = "https://idp.example.com",
                ClientId = "oidc-test-client",
                ClientSecret = "test-oidc-secret",
            },
        };

        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<User>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<Role>?>(),
                It.IsAny<bool>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>()))
            .Returns("access-token");
        tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");
        tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        return new ExternalAuthService(
            db,
            tokenService.Object,
            Config(selfHostedTenantId),
            Mock.Of<IContactService>(),
            new MemoryCache(new MemoryCacheOptions()),
            BuildOidcHttpClientFactory(),
            Options.Create(settings),
            NullLogger<ExternalAuthService>.Instance);
    }

    /// <summary>
    /// Drives a real OIDC login callback, which is the only way in to the private
    /// <c>FindOrCreateUserAsync</c>. The state is minted through the service's own
    /// <c>GetAuthorizationUrlAsync</c> rather than planted in the cache, so the test exercises the
    /// same one-time-use state the production flow does.
    /// </summary>
    private static async Task ProcessOidcCallbackAsync(ExternalAuthService sut, string email)
    {
        var challenge = await sut.GetAuthorizationUrlAsync(
            "OIDC", "https://app.famick.com/callback", CancellationToken.None);

        await sut.ProcessCallbackAsync(
            "OIDC",
            // The code doubles as the identity hint for the stub token endpoint — see
            // ExtractEmailHint. Production codes are opaque.
            new ExternalAuthCallbackRequest { Code = email, State = challenge.State },
            "https://app.famick.com/callback",
            "127.0.0.1",
            "tests",
            CancellationToken.None);
    }

    private static IHttpClientFactory BuildOidcHttpClientFactory()
    {
        // Discovery, then a token endpoint whose id_token carries the identity under test. The
        // service reads that token with JwtSecurityTokenHandler.ReadJwtToken, which does not
        // verify the signature, but it is signed anyway so the stub stays honest if that changes.
        var handler = new StubHttpMessageHandler(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            if (path.EndsWith("/.well-known/openid-configuration"))
            {
                return Json("""
                    {
                      "issuer": "https://idp.example.com",
                      "authorization_endpoint": "https://idp.example.com/authorize",
                      "token_endpoint": "https://idp.example.com/token",
                      "userinfo_endpoint": "https://idp.example.com/userinfo",
                      "jwks_uri": "https://idp.example.com/jwks"
                    }
                    """);
            }

            if (path == "/token")
            {
                // The callback helper puts the email in the form body's state-independent code;
                // simplest honest stub is to read it back off the request we were handed.
                var form = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                var email = ExtractEmailHint(form);
                return Json($$"""{"id_token":"{{IdToken(email)}}"}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handler));
        return factory.Object;
    }

    /// <summary>
    /// The authorization code carries the email so one stub handler serves every test. Production
    /// codes are opaque; this is a test convention, not a shape the service depends on.
    /// </summary>
    private static string ExtractEmailHint(string form)
    {
        const string marker = "code=";
        var start = form.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = form.IndexOf('&', start);
        var code = Uri.UnescapeDataString(end < 0 ? form[start..] : form[start..end]);
        return code.Contains('@') ? code : "operator@example.com";
    }

    private static string IdToken(string email)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64)));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "https://idp.example.com",
            Audience = "oidc-test-client",
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", $"oidc-sub-for-{email}"),
                new Claim("email", email),
                new Claim("name", "Resident Example"),
                new Claim("given_name", "Resident"),
                new Claim("family_name", "Example"),
            ]),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    // --- Site 2 harness ---

    private static AuthenticationService BuildAuthenticationService(
        HomeManagementDbContext db,
        string? selfHostedTenantId)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed_password");

        return new AuthenticationService(
            db,
            hasher.Object,
            Mock.Of<ITokenService>(),
            Config(selfHostedTenantId),
            Mock.Of<IContactService>(),
            Mock.Of<IJwtMinIatService>(),
            Mock.Of<IUserAdvisoryLockService>(),
            NullLogger<AuthenticationService>.Instance);
    }

    private static RegisterRequest NewRegisterRequest() => new()
    {
        Email = "operator@example.com",
        Password = "correct horse battery staple",
        ConfirmPassword = "correct horse battery staple",
        FirstName = "Operator",
        LastName = "Example",
    };

    // --- Site 3 harness ---

    private static UserManagementService BuildUserManagementService(
        HomeManagementDbContext db,
        Guid? resolvedTenantId)
    {
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.HashPassword(It.IsAny<string>())).Returns("hashed_password");

        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(p => p.TenantId).Returns(resolvedTenantId);

        return new UserManagementService(
            db,
            hasher.Object,
            Mock.Of<IEmailService>(),
            Mock.Of<IMessageService>(),
            tenantProvider.Object,
            Mock.Of<IContactService>(),
            NullLogger<UserManagementService>.Instance);
    }

    private static CreateUserRequest NewCreateUserRequest() => new()
    {
        Email = "member@example.com",
        FirstName = "Member",
        LastName = "Example",
        Roles = [Role.Admin, Role.Editor],
        SendWelcomeEmail = false,
    };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(send(request));
    }
}
