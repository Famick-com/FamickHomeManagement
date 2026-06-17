using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Famick.HomeManagement.Tests.Unit.Authentication;

/// <summary>
/// AuthenticationService.IssueSessionAsync backs the HA Ingress SSO endpoint:
/// it mints a session for an already-resolved user with no password check, so
/// it must still enforce existence + active state and persist a refresh token.
/// </summary>
public class HaIngressSessionIssuerTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (IHaIngressSessionIssuer Issuer, HomeManagementDbContext Db) Build()
    {
        var db = new HomeManagementDbContext(new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"ha-session-{Guid.NewGuid()}")
            .Options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(t => t.GenerateAccessToken(
                It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Role>?>(),
                It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Returns("access-token");
        tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token-raw");
        tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddMinutes(15));

        var service = new AuthenticationService(
            context: db,
            passwordHasher: Mock.Of<IPasswordHasher>(),
            tokenService: tokenService.Object,
            configuration: config,
            contactService: Mock.Of<IContactService>(),
            jwtMinIatService: Mock.Of<IJwtMinIatService>(),
            userLockService: Mock.Of<IUserAdvisoryLockService>(),
            logger: NullLogger<AuthenticationService>.Instance,
            multiTenancyOptions: Mock.Of<IMultiTenancyOptions>(o => o.IsMultiTenantEnabled == false),
            localServerResolver: null);

        return (service, db);
    }

    private static User SeedUser(HomeManagementDbContext db, bool active)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Email = "alice@ha-ingress.local",
            Username = "alice",
            FirstName = "Alice",
            LastName = "User",
            PasswordHash = string.Empty,
            IsActive = active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = user.Id,
            Role = Role.Admin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return user;
    }

    [Fact]
    public async Task IssueSessionAsync_ActiveUser_IssuesTokensAndPersistsRefreshToken()
    {
        var (issuer, db) = Build();
        var user = SeedUser(db, active: true);

        var response = await issuer.IssueSessionAsync(user.Id, "172.30.32.1", "ua");

        response.AccessToken.Should().Be("access-token");
        response.RefreshToken.Should().Be("refresh-token-raw");
        response.User.Id.Should().Be(user.Id);
        // A hashed refresh token row is persisted for the user (raw token never stored).
        var stored = await db.RefreshTokens.SingleAsync(rt => rt.UserId == user.Id);
        stored.TokenHash.Should().NotBe("refresh-token-raw").And.NotBeNullOrEmpty();
    }

    [Fact]
    public async Task IssueSessionAsync_UnknownUser_Throws()
    {
        var (issuer, _) = Build();

        await FluentActions.Awaiting(() => issuer.IssueSessionAsync(Guid.NewGuid(), "ip", "ua"))
            .Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task IssueSessionAsync_InactiveUser_Throws()
    {
        var (issuer, db) = Build();
        var user = SeedUser(db, active: false);

        await FluentActions.Awaiting(() => issuer.IssueSessionAsync(user.Id, "ip", "ua"))
            .Should().ThrowAsync<AccountInactiveException>();
    }
}
