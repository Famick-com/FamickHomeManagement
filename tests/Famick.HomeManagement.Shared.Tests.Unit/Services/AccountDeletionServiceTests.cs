using Famick.HomeManagement.Core.DTOs.Account;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Account deletion exists because App Store Review Guideline 5.1.1(v) requires it, but
/// the behaviour that matters is what a request destroys. An admin's request takes the
/// household; a member's takes only themselves. Getting that backwards would let one
/// person delete data other people put in.
/// </summary>
public class AccountDeletionServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _context;
    private readonly Mock<IJwtMinIatService> _jwtMinIat = new();
    private readonly AccountDeletionService _service;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _adminId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();

    public AccountDeletionServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new HomeManagementDbContext(options);

        _context.Tenants.Add(new Tenant { Id = _tenantId, Name = "The Therien Family" });
        _context.Users.AddRange(
            new User { Id = _adminId, TenantId = _tenantId, Email = "admin@example.com", Username = "admin" },
            new User { Id = _memberId, TenantId = _tenantId, Email = "member@example.com", Username = "member" });
        _context.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = _adminId, Role = Role.Admin
        });
        _context.UserRoles.Add(new UserRole
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = _memberId, Role = Role.Editor
        });
        _context.SaveChanges();

        _service = new AccountDeletionService(
            _context, _jwtMinIat.Object, Mock.Of<ILogger<AccountDeletionService>>());
    }

    public void Dispose() => _context.Dispose();

    // ---------- what a request destroys ----------

    [Fact]
    public async Task AdminRequestSchedulesTheWholeHousehold()
    {
        var result = await _service.RequestAsync(_adminId);

        result.Scope.Should().Be(AccountDeletionScope.Household);
        result.OtherMembersAffected.Should().Be(1, "the member loses access too");

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == _tenantId);
        tenant.DeletionRequestedAt.Should().NotBeNull();
        tenant.DeletionRequestedByUserId.Should().Be(_adminId);
    }

    [Fact]
    public async Task MemberRequestLeavesTheHouseholdAlone()
    {
        var result = await _service.RequestAsync(_memberId);

        result.Scope.Should().Be(AccountDeletionScope.User);
        result.OtherMembersAffected.Should().Be(0);

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == _tenantId);
        tenant.DeletionRequestedAt.Should().BeNull("a member leaving must not close the household");

        var member = await _context.Users.FirstAsync(u => u.Id == _memberId);
        member.DeletionRequestedAt.Should().NotBeNull();

        var admin = await _context.Users.FirstAsync(u => u.Id == _adminId);
        admin.DeletionRequestedAt.Should().BeNull("the other member is untouched");
    }

    [Fact]
    public async Task PurgeAfterIsThirtyDaysOut()
    {
        var result = await _service.RequestAsync(_memberId);

        (result.PurgeAfter - result.RequestedAt)
            .Should().BeCloseTo(AccountDeletionService.GracePeriod, TimeSpan.FromSeconds(5));
    }

    // ---------- ending sessions ----------

    [Fact]
    public async Task RequestingRevokesRefreshTokensAndInvalidatesAccessTokens()
    {
        _context.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = _memberId,
            TokenHash = "hash", ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _context.SaveChangesAsync();

        await _service.RequestAsync(_memberId);

        (await _context.RefreshTokens.FirstAsync(t => t.UserId == _memberId))
            .IsRevoked.Should().BeTrue();

        // Without the min-iat bump an access token minted seconds earlier would still be
        // accepted, and the cancel-on-return rule would read it as the user coming back.
        _jwtMinIat.Verify(s => s.BumpAsync(_memberId, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminRequestEndsEveryMembersSessions()
    {
        await _service.RequestAsync(_adminId);

        _jwtMinIat.Verify(s => s.BumpAsync(_adminId, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
        _jwtMinIat.Verify(s => s.BumpAsync(_memberId, It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- cancelling ----------

    [Fact]
    public async Task AdminCanCallOffAHouseholdDeletion()
    {
        await _service.RequestAsync(_adminId);

        (await _service.CancelAsync(_adminId)).Should().BeTrue();

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == _tenantId);
        tenant.DeletionRequestedAt.Should().BeNull();
        tenant.DeletionPurgeAfter.Should().BeNull();
    }

    [Fact]
    public async Task MemberCannotCallOffAHouseholdDeletion()
    {
        await _service.RequestAsync(_adminId);

        await _service.CancelAsync(_memberId);

        var tenant = await _context.Tenants.FirstAsync(t => t.Id == _tenantId);
        tenant.DeletionRequestedAt.Should().NotBeNull(
            "reversing a decision that was not theirs would let any member veto the admin");
    }

    [Fact]
    public async Task CancellingNothingReportsNothing()
    {
        (await _service.CancelAsync(_memberId)).Should().BeFalse();
    }

    // ---------- returning ----------

    [Fact]
    public async Task ReturningAfterTheRequestCancelsIt()
    {
        await _service.RequestAsync(_memberId);

        var freshToken = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var decision = await _service.ReconcileAuthenticatedRequestAsync(_memberId, freshToken);

        decision.Should().Be(AccountAccessDecision.Allow);
        (await _context.Users.FirstAsync(u => u.Id == _memberId))
            .DeletionRequestedAt.Should().BeNull();
    }

    [Fact]
    public async Task AStaleTokenDoesNotCountAsReturning()
    {
        await _service.RequestAsync(_memberId);

        // Issued before the request, so it is not evidence of anyone coming back. The
        // min-iat bump should already have killed it; this is the second line of defence.
        var staleToken = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        await _service.ReconcileAuthenticatedRequestAsync(_memberId, staleToken);

        (await _context.Users.FirstAsync(u => u.Id == _memberId))
            .DeletionRequestedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MemberIsRefusedWhileTheHouseholdIsPending()
    {
        await _service.RequestAsync(_adminId);

        var decision = await _service.ReconcileAuthenticatedRequestAsync(
            _memberId, DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());

        decision.Should().Be(AccountAccessDecision.HouseholdDeletionPending);
    }

    [Fact]
    public async Task AdminReturningReopensTheHouseholdForEveryone()
    {
        await _service.RequestAsync(_adminId);

        await _service.ReconcileAuthenticatedRequestAsync(
            _adminId, DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());

        var memberDecision = await _service.ReconcileAuthenticatedRequestAsync(
            _memberId, DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds());

        memberDecision.Should().Be(AccountAccessDecision.Allow);
    }

    [Fact]
    public async Task NothingPendingIsAllowed()
    {
        var decision = await _service.ReconcileAuthenticatedRequestAsync(
            _memberId, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        decision.Should().Be(AccountAccessDecision.Allow);
    }

    // ---------- status ----------

    [Fact]
    public async Task StatusWarnsAnAdminThatTheHouseholdGoes()
    {
        var status = await _service.GetStatusAsync(_adminId);

        status.IsPending.Should().BeFalse();
        status.Scope.Should().Be(AccountDeletionScope.Household);
        status.HouseholdName.Should().Be("The Therien Family");
        status.OtherMemberCount.Should().Be(1);
    }

    [Fact]
    public async Task StatusTellsAMemberOnlyTheyGo()
    {
        var status = await _service.GetStatusAsync(_memberId);

        status.Scope.Should().Be(AccountDeletionScope.User);
    }

    [Fact]
    public async Task StatusReportsAPendingHouseholdDeletionToMembersToo()
    {
        await _service.RequestAsync(_adminId);

        var status = await _service.GetStatusAsync(_memberId);

        status.IsPending.Should().BeTrue();
        status.Scope.Should().Be(AccountDeletionScope.Household,
            "the member's account is going because the household is, and the UI should say so");
    }

    // ---------- purge ordering ----------

    /// <summary>
    /// The household purge deletes table by table in an order computed from the model.
    /// 11 of the model's relationships are Restrict, so a wrong order fails at the moment
    /// someone closes their household — the worst possible time to find out.
    /// </summary>
    /// <remarks>
    /// Only required relationships are asserted, because only they constrain the order.
    /// Optional ones are nulled before anything is deleted, and
    /// <see cref="EveryCyclicReferenceIsOptionalAndThereforeBreakable"/> is what checks
    /// that assumption still holds.
    /// </remarks>
    [Fact]
    public void DeleteOrderSatisfiesEveryRequiredForeignKey()
    {
        using var context = RelationalModelContext();
        var order = AccountDeletionService.TenantEntityTypesInDeleteOrder(context.Model);

        var position = order
            .Select((type, index) => (type, index))
            .ToDictionary(x => x.type, x => x.index);

        var violations = new List<string>();

        foreach (var dependent in order)
        {
            foreach (var fk in dependent.GetForeignKeys().Where(fk => fk.IsRequired))
            {
                var principal = fk.PrincipalEntityType;
                if (principal == dependent) continue;              // self-reference, same table
                if (!position.ContainsKey(principal)) continue;    // points outside the tenant set

                if (position[dependent] > position[principal])
                {
                    violations.Add(
                        $"{dependent.ClrType.Name} -> {principal.ClrType.Name} " +
                        $"(deleted at {position[dependent]}, after {position[principal]})");
                }
            }
        }

        violations.Should().BeEmpty(
            "every table must be deleted before the tables it requires");
    }

    /// <summary>
    /// The ordering can only exist because every cycle in the model runs through an
    /// optional reference that the purge nulls out first — User and Contact point at each
    /// other, and neither could go first otherwise.
    /// </summary>
    /// <remarks>
    /// A cycle made entirely of required references would be unorderable, and the purge
    /// would fail against a live database while every unit test still passed. This finds
    /// that before it ships.
    /// </remarks>
    [Fact]
    public void EveryCyclicReferenceIsOptionalAndThereforeBreakable()
    {
        using var context = RelationalModelContext();

        var tenantTypes = context.Model.GetEntityTypes()
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetTableName() != null)
            .ToHashSet();

        // Edges that survive the nulling pass, i.e. the required ones.
        var edges = tenantTypes.ToDictionary(
            t => t,
            t => t.GetForeignKeys()
                .Where(fk => fk.IsRequired)
                .Select(fk => fk.PrincipalEntityType)
                .Where(p => p != t && tenantTypes.Contains(p))
                .Distinct()
                .ToList());

        var state = new Dictionary<IEntityType, int>();
        var cycles = new List<string>();

        bool HasCycle(IEntityType type, List<IEntityType> path)
        {
            state[type] = 1;
            path.Add(type);

            foreach (var next in edges[type])
            {
                var seen = state.GetValueOrDefault(next);
                if (seen == 1)
                {
                    cycles.Add(string.Join(" -> ", path.Select(p => p.ClrType.Name)) + $" -> {next.ClrType.Name}");
                    return true;
                }
                if (seen == 0 && HasCycle(next, path)) return true;
            }

            path.RemoveAt(path.Count - 1);
            state[type] = 2;
            return false;
        }

        foreach (var type in tenantTypes)
        {
            if (state.GetValueOrDefault(type) == 0)
                HasCycle(type, new List<IEntityType>());
        }

        cycles.Should().BeEmpty(
            "a cycle of required references cannot be ordered, so the household purge would fail");
    }

    [Fact]
    public void DeleteOrderCoversEveryTenantScopedEntity()
    {
        using var context = RelationalModelContext();
        var order = AccountDeletionService.TenantEntityTypesInDeleteOrder(context.Model);

        var expected = context.Model.GetEntityTypes()
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetTableName() != null)
            .ToList();

        // A tenant entity missing from the order is a table left behind after the
        // household is deleted — rows nobody can reach and nobody deletes.
        order.Should().HaveCount(expected.Count);
        order.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// The in-memory provider carries no relational metadata, so table names and the
    /// full foreign-key graph are only present under a relational provider. No connection
    /// is opened — the model is built from the configuration alone.
    /// </summary>
    private static HomeManagementDbContext RelationalModelContext()
    {
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseNpgsql("Host=localhost;Database=model_only")
                .Options);
    }
}
