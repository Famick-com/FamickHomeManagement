using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Famick.HomeManagement.Shared.Tests.Unit.Services;

/// <summary>
/// Per-user cloud-login opt-in service. Uses InMemory EF + a fake
/// <see cref="ITunnelSender"/> that records all frames the service
/// attempted to push, so we can verify both the DB state machine and
/// the wire-level interactions.
/// </summary>
public class CloudLoginOptInServiceTests : IDisposable
{
    private readonly HomeManagementDbContext _db;
    private readonly Guid _tenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public CloudLoginOptInServiceTests()
    {
        var options = new DbContextOptionsBuilder<HomeManagementDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new HomeManagementDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task OptInAsync_inserts_row_and_pushes_USER_REGISTER()
    {
        var userId = await SeedUserAsync("alice@example.com");
        var sender = new RecordingTunnelSender(connected: true);
        var sut = BuildSut(sender);

        await sut.OptInAsync(userId, CancellationToken.None);

        (await _db.UserCloudLoginOptIns.CountAsync()).Should().Be(1);
        sender.Sent.Should().ContainSingle().Which.Should().BeOfType<UserRegister>()
            .Which.Email.Should().Be("alice@example.com");
    }

    [Fact]
    public async Task OptInAsync_is_idempotent_replays_the_register_push()
    {
        var userId = await SeedUserAsync("bob@example.com");
        var sender = new RecordingTunnelSender(connected: true);
        var sut = BuildSut(sender);

        await sut.OptInAsync(userId, CancellationToken.None);
        await sut.OptInAsync(userId, CancellationToken.None);

        // Still one row; the second call replays USER_REGISTER as a
        // best-effort resync if local-vs-AuthProxy drifted.
        (await _db.UserCloudLoginOptIns.CountAsync()).Should().Be(1);
        sender.Sent.Should().HaveCount(2);
        sender.Sent.OfType<UserRegister>().Should().HaveCount(2);
    }

    [Fact]
    public async Task OptOutAsync_removes_row_and_pushes_USER_UNREGISTER()
    {
        var userId = await SeedUserAsync("carol@example.com");
        var sender = new RecordingTunnelSender(connected: true);
        var sut = BuildSut(sender);

        await sut.OptInAsync(userId, CancellationToken.None);
        sender.Sent.Clear();

        await sut.OptOutAsync(userId, CancellationToken.None);

        (await _db.UserCloudLoginOptIns.CountAsync()).Should().Be(0);
        sender.Sent.Should().ContainSingle().Which.Should().BeOfType<UserUnregister>()
            .Which.Email.Should().Be("carol@example.com");
    }

    [Fact]
    public async Task OptOutAsync_is_safe_when_not_opted_in()
    {
        var userId = await SeedUserAsync("dave@example.com");
        var sender = new RecordingTunnelSender(connected: true);
        var sut = BuildSut(sender);

        await sut.OptOutAsync(userId, CancellationToken.None);

        (await _db.UserCloudLoginOptIns.CountAsync()).Should().Be(0);
        // Still pushes UNREGISTER — defensive replay.
        sender.Sent.Should().ContainSingle().Which.Should().BeOfType<UserUnregister>();
    }

    [Fact]
    public async Task OptInAsync_persists_row_even_when_tunnel_offline()
    {
        var userId = await SeedUserAsync("eve@example.com");
        var sender = new RecordingTunnelSender(connected: false);
        var sut = BuildSut(sender);

        await sut.OptInAsync(userId, CancellationToken.None);

        (await _db.UserCloudLoginOptIns.CountAsync()).Should().Be(1,
            "opt-in is local-first; the next USER_SYNC at reconnect catches AuthProxy up");
        // Sender was called but TrySendAsync returned false; nothing recorded.
        sender.Sent.Should().BeEmpty();
        sender.TryCalls.Should().Be(1);
    }

    [Fact]
    public async Task IsOptedInAsync_reflects_current_state()
    {
        var userId = await SeedUserAsync("frank@example.com");
        var sut = BuildSut(new RecordingTunnelSender(connected: true));

        (await sut.IsOptedInAsync(userId, CancellationToken.None)).Should().BeFalse();
        await sut.OptInAsync(userId, CancellationToken.None);
        (await sut.IsOptedInAsync(userId, CancellationToken.None)).Should().BeTrue();
        await sut.OptOutAsync(userId, CancellationToken.None);
        (await sut.IsOptedInAsync(userId, CancellationToken.None)).Should().BeFalse();
    }

    [Fact]
    public async Task GetOptedInEmailsAsync_returns_only_opted_in_users()
    {
        var opted1 = await SeedUserAsync("opt-in-1@example.com");
        var opted2 = await SeedUserAsync("opt-in-2@example.com");
        await SeedUserAsync("not-opted@example.com");  // never opts in

        var sut = BuildSut(new RecordingTunnelSender(connected: true));
        await sut.OptInAsync(opted1, CancellationToken.None);
        await sut.OptInAsync(opted2, CancellationToken.None);

        var emails = await sut.GetOptedInEmailsAsync(CancellationToken.None);

        emails.Should().BeEquivalentTo(new[] { "opt-in-1@example.com", "opt-in-2@example.com" });
    }

    [Fact]
    public async Task OptInAsync_throws_when_user_not_found()
    {
        var sut = BuildSut(new RecordingTunnelSender(connected: true));

        var act = () => sut.OptInAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---- helpers ----

    private async Task<Guid> SeedUserAsync(string email)
    {
        var id = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = id,
            TenantId = _tenantId,
            Email = email,
            Username = email,
            FirstName = "Test",
            LastName = "User",
            PasswordHash = "x",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return id;
    }

    private CloudLoginOptInService BuildSut(RecordingTunnelSender sender)
    {
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.SetupGet(t => t.TenantId).Returns(_tenantId);
        return new CloudLoginOptInService(
            _db,
            tenantProvider.Object,
            sender,
            NullLogger<CloudLoginOptInService>.Instance);
    }

    private sealed class RecordingTunnelSender : ITunnelSender
    {
        private readonly bool _connected;
        public List<TunnelEnvelope> Sent { get; } = new();
        public int TryCalls { get; private set; }

        public RecordingTunnelSender(bool connected) => _connected = connected;

        public Task<bool> TrySendAsync(TunnelEnvelope envelope, CancellationToken ct = default)
        {
            TryCalls++;
            if (!_connected) return Task.FromResult(false);
            Sent.Add(envelope);
            return Task.FromResult(true);
        }
    }
}
