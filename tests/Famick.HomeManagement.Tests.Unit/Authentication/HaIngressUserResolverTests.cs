using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Famick.HomeManagement.Tests.Unit.Authentication;

public class HaIngressUserResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static (HaIngressUserResolver Resolver, HomeManagementDbContext Db) Build()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase($"haingress-{Guid.NewGuid()}")
            .Options;
        var db = new HomeManagementDbContext(options);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SelfHosted:TenantId"] = TenantId.ToString(),
            })
            .Build();
        var resolver = new HaIngressUserResolver(db, config, NullLogger<HaIngressUserResolver>.Instance);
        return (resolver, db);
    }

    [Fact]
    public async Task ResolveAsync_FirstUser_BecomesAdminWithSyntheticEmail()
    {
        var (resolver, db) = Build();

        var user = await resolver.ResolveAsync(new HaIngressIdentity(
            HaUserId: "abc-123",
            Username: "alice",
            DisplayName: "Alice Anderson"));

        user.Email.Should().Be("abc-123@ha-ingress.local");
        user.Username.Should().Be("alice");
        user.FirstName.Should().Be("Alice");
        user.LastName.Should().Be("Anderson");
        user.TenantId.Should().Be(TenantId);
        user.IsActive.Should().BeTrue();

        var role = await db.UserRoles.SingleAsync();
        role.UserId.Should().Be(user.Id);
        role.Role.Should().Be(Role.Admin);

        var link = await db.UserExternalLogins.SingleAsync();
        link.Provider.Should().Be("ha-ingress");
        link.ProviderUserId.Should().Be("abc-123");
        link.ProviderDisplayName.Should().Be("Alice Anderson");
        link.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ResolveAsync_SubsequentUser_GetsEditorRole()
    {
        var (resolver, db) = Build();

        var first = await resolver.ResolveAsync(new HaIngressIdentity("user-1", null, null));
        var second = await resolver.ResolveAsync(new HaIngressIdentity("user-2", null, null));

        first.Id.Should().NotBe(second.Id);
        var roles = await db.UserRoles.OrderBy(r => r.CreatedAt).ToListAsync();
        roles.Should().HaveCount(2);
        roles[0].Role.Should().Be(Role.Admin);
        roles[1].Role.Should().Be(Role.Editor);
    }

    [Fact]
    public async Task ResolveAsync_ReturningUser_DoesNotCreateNewRows()
    {
        var (resolver, db) = Build();

        var first = await resolver.ResolveAsync(new HaIngressIdentity("same-id", "n", "Old Name"));
        var second = await resolver.ResolveAsync(new HaIngressIdentity("same-id", "n", "Old Name"));

        first.Id.Should().Be(second.Id);
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.UserExternalLogins.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ResolveAsync_ReturningUser_RefreshesDisplayName()
    {
        var (resolver, db) = Build();

        await resolver.ResolveAsync(new HaIngressIdentity("id", null, "Old Name"));
        await resolver.ResolveAsync(new HaIngressIdentity("id", null, "New Name"));

        var link = await db.UserExternalLogins.SingleAsync();
        link.ProviderDisplayName.Should().Be("New Name");
    }

    [Fact]
    public async Task ResolveAsync_BlankHaUserId_Throws()
    {
        var (resolver, _) = Build();

        await FluentActions.Invoking(() => resolver.ResolveAsync(new HaIngressIdentity("", null, null)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ResolveAsync_EmailLocalPartMatchesExistingUser_LinksWithoutCreatingNew()
    {
        var (resolver, db) = Build();
        var existing = SeedUser(db, email: "alice@example.com", role: Role.Editor);

        var resolved = await resolver.ResolveAsync(new HaIngressIdentity(
            HaUserId: "ha-alice",
            Username: "alice",
            DisplayName: "Alice A."));

        resolved.Id.Should().Be(existing.Id);
        (await db.Users.CountAsync()).Should().Be(1);
        (await db.UserRoles.CountAsync()).Should().Be(1);
        var link = await db.UserExternalLogins.SingleAsync();
        link.UserId.Should().Be(existing.Id);
        link.ProviderUserId.Should().Be("ha-alice");
        link.ProviderDisplayName.Should().Be("Alice A.");
    }

    [Fact]
    public async Task ResolveAsync_EmailLocalPartMatchIsCaseInsensitive()
    {
        var (resolver, db) = Build();
        var existing = SeedUser(db, email: "Alice@Example.com", role: Role.Editor);

        var resolved = await resolver.ResolveAsync(new HaIngressIdentity(
            HaUserId: "ha-alice",
            Username: "alice",
            DisplayName: null));

        resolved.Id.Should().Be(existing.Id);
        (await db.Users.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ResolveAsync_AmbiguousLocalPart_FallsThroughToNewUser()
    {
        // alice@a.com and alice@b.com both match HA username "alice" — can't
        // tell which is the real one, so don't auto-link either; provision a
        // fresh HA-Ingress user instead.
        var (resolver, db) = Build();
        SeedUser(db, email: "alice@a.com", role: Role.Admin);
        SeedUser(db, email: "alice@b.com", role: Role.Editor);

        var resolved = await resolver.ResolveAsync(new HaIngressIdentity(
            HaUserId: "ha-alice",
            Username: "alice",
            DisplayName: null));

        resolved.Email.Should().Be("ha-alice@ha-ingress.local");
        (await db.Users.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task ResolveAsync_DifferentLocalPart_CreatesNewEditor()
    {
        var (resolver, db) = Build();
        SeedUser(db, email: "alice@example.com", role: Role.Admin);

        var resolved = await resolver.ResolveAsync(new HaIngressIdentity(
            HaUserId: "ha-bob",
            Username: "bob",
            DisplayName: "Bob B."));

        resolved.Email.Should().Be("ha-bob@ha-ingress.local");
        (await db.Users.CountAsync()).Should().Be(2);
        var bobRole = await db.UserRoles.SingleAsync(r => r.UserId == resolved.Id);
        bobRole.Role.Should().Be(Role.Editor);
    }

    [Fact]
    public async Task ResolveAsync_PrefixOnlyDifferentLocalPart_DoesNotFalsePositive()
    {
        // "alicia@..." starts with "alic" but not "alice@", so HA "alice"
        // must not link to it.
        var (resolver, db) = Build();
        SeedUser(db, email: "alicia@example.com", role: Role.Admin);

        var resolved = await resolver.ResolveAsync(new HaIngressIdentity(
            HaUserId: "ha-alice",
            Username: "alice",
            DisplayName: null));

        resolved.Email.Should().Be("ha-alice@ha-ingress.local");
        (await db.Users.CountAsync()).Should().Be(2);
    }

    private static User SeedUser(HomeManagementDbContext db, string email, Role role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Email = email,
            Username = email,
            FirstName = email.Split('@')[0],
            LastName = "Seed",
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            UserId = user.Id,
            Role = role,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return user;
    }
}
