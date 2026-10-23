using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// A Google/Apple/OIDC sign-in answers with the same terms flag and household subscription
/// state as a password sign-in, so the client routes to accept-terms and gates features from
/// its first screen. Driven through <see cref="ExternalAuthService.SignInLinkedUserAsync"/>,
/// which shares the response builder with the callback and native paths.
/// </summary>
public class ExternalAuthServiceLoginResponseTests
{
    private const string CurrentTerms = "2026-10";
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly string _dbName = $"external-login-response-{Guid.NewGuid()}";
    private bool? _tokenMustAcceptTerms;

    [Theory]
    [InlineData(null)]
    [InlineData("2025-01")]
    public async Task Cloud_sign_in_with_outdated_terms_must_accept_them(string? acceptedVersion)
    {
        var user = Seed(acceptedVersion);

        var response = await SignInAsync(user, cloud: true);

        response.MustAcceptTerms.Should().BeTrue();
        _tokenMustAcceptTerms.Should().BeTrue("the middleware enforces the claim, not the response flag");
    }

    [Fact]
    public async Task Cloud_sign_in_with_current_terms_goes_straight_in()
    {
        var user = Seed(CurrentTerms);

        var response = await SignInAsync(user, cloud: true);

        response.MustAcceptTerms.Should().BeFalse();
        _tokenMustAcceptTerms.Should().BeFalse();
    }

    [Fact]
    public async Task Self_hosted_sign_in_never_asks_for_terms()
    {
        var user = Seed(acceptedVersion: null);

        var response = await SignInAsync(user, cloud: false);

        response.MustAcceptTerms.Should().BeFalse();
        _tokenMustAcceptTerms.Should().BeFalse();
    }

    [Fact]
    public async Task Cloud_sign_in_carries_the_households_subscription()
    {
        var trialEnds = DateTime.UtcNow.AddDays(10);
        var user = Seed(CurrentTerms, SubscriptionTier.Organize, trialEnds);

        var response = await SignInAsync(user, cloud: true);

        response.Tenant.Should().NotBeNull();
        response.Tenant!.Id.Should().Be(TenantId);
        response.Tenant.Name.Should().Be("Test Household");
        response.Tenant.SubscriptionTier.Should().Be("Organize");
        response.Tenant.IsTrialActive.Should().BeTrue();
        response.Tenant.TrialEndsAt.Should().Be(trialEnds);
        response.Tenant.IsExpired.Should().BeFalse();
    }

    [Fact]
    public async Task Cloud_sign_in_reports_a_free_household_past_its_trial_as_expired()
    {
        var user = Seed(CurrentTerms, SubscriptionTier.Free, DateTime.UtcNow.AddDays(-1));

        var response = await SignInAsync(user, cloud: true);

        response.Tenant!.SubscriptionTier.Should().Be("Free");
        response.Tenant.IsTrialActive.Should().BeFalse();
        response.Tenant.IsExpired.Should().BeTrue();
    }

    [Fact]
    public async Task Self_hosted_sign_in_unlocks_every_feature()
    {
        var user = Seed(acceptedVersion: null, SubscriptionTier.Free, trialEndsAt: null);

        var response = await SignInAsync(user, cloud: false);

        response.Tenant.Should().NotBeNull();
        response.Tenant!.Id.Should().Be(TenantId);
        response.Tenant.IsUnrestricted.Should().BeTrue();
        response.Tenant.SubscriptionTier.Should().BeEmpty("a self-hosted household has no plan");
        response.Tenant.IsTrialActive.Should().BeFalse();
        response.Tenant.IsExpired.Should().BeFalse();
    }

    // --- harness ---

    private Task<Core.DTOs.Authentication.LoginResponse> SignInAsync(User user, bool cloud) =>
        BuildService(cloud).SignInLinkedUserAsync(
            user.Id, "Google", "google-subject", rememberMe: false, "127.0.0.1", "tests");

    private User Seed(
        string? acceptedVersion,
        SubscriptionTier tier = SubscriptionTier.Home,
        DateTime? trialEndsAt = null)
    {
        using var db = NewDb();
        db.Tenants.Add(new Tenant
        {
            Id = TenantId, Name = "Test Household", CreatedAt = DateTime.UtcNow,
            SubscriptionTier = tier, TrialEndsAt = trialEndsAt,
        });
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = TenantId,
            Email = "resident@example.com", Username = "resident@example.com",
            FirstName = "Resident", LastName = "Example", PasswordHash = string.Empty,
            TermsAcceptedAt = acceptedVersion is null ? null : DateTime.UtcNow.AddDays(-30),
            TermsVersion = acceptedVersion,
            IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        db.UserExternalLogins.Add(new UserExternalLogin
        {
            Id = Guid.NewGuid(), TenantId = TenantId, UserId = user.Id,
            Provider = "Google", ProviderUserId = "google-subject",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });
        db.SaveChanges();
        return user;
    }

    private ExternalAuthService BuildService(bool cloud)
    {
        var tokenService = new Mock<ITokenService>();
        tokenService
            .Setup(t => t.GenerateAccessToken(
                It.IsAny<User>(), It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<Role>?>(),
                It.IsAny<bool>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .Callback((User _, IEnumerable<string> _, IEnumerable<Role>? _, bool mustAcceptTerms, DateTime? _, DateTime? _) =>
                _tokenMustAcceptTerms = mustAcceptTerms)
            .Returns("access-token");
        tokenService.Setup(t => t.GenerateRefreshToken()).Returns("refresh-token");
        tokenService.Setup(t => t.GetTokenExpiration()).Returns(DateTime.UtcNow.AddHours(1));

        // Both modes publish a terms version so self-hosted is shown to ignore it.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["LegalTerms:CurrentVersion"] = CurrentTerms })
            .Build();

        return new ExternalAuthService(
            NewDb(), tokenService.Object, configuration,
            new MultiTenancyOptions { IsMultiTenantEnabled = cloud },
            Mock.Of<IContactService>(), new MemoryCache(new MemoryCacheOptions()), new MemoryOneTimeStore(),
            Mock.Of<IHttpClientFactory>(), Options.Create(new ExternalAuthSettings()),
            NullLogger<ExternalAuthService>.Instance);
    }

    private HomeManagementDbContext NewDb() =>
        new(new DbContextOptionsBuilder<HomeManagementDbContext>().UseInMemoryDatabase(_dbName).Options);
}
