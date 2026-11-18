using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.TestSupport.Containers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Famick.HomeManagement.Shared.Tests.Integration.Services;

/// <summary>
/// A contact saved inside a transaction is reported only once the transaction commits, and never
/// if it rolls back. Against Postgres because the in-memory provider has no transactions.
/// </summary>
public class ContactChangeInterceptorPostgresTests(PostgresContainerFixture fixture)
    : IClassFixture<PostgresContainerFixture>, IAsyncLifetime
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly RecordingSink _sink = new();
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        _services = new ServiceCollection().AddSingleton<IContactChangeSink>(_sink).BuildServiceProvider();

        await using var db = Context();
        db.Tenants.Add(new Tenant { Id = _tenantId, Name = "Household" });
        db.Users.Add(new User
        {
            Id = _userId, TenantId = _tenantId, Username = $"u-{_userId:N}", Email = $"{_userId:N}@example.com",
            FirstName = "Test", LastName = "User", PasswordHash = "x", IsActive = true,
        });
        await db.SaveChangesAsync();
        _sink.Reported.Clear();
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    [Fact]
    public async Task ASaveInsideATransactionIsReportedWhenItCommits()
    {
        await using var db = Context();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var contact = NewContact();
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();

        _sink.Reported.Should().BeEmpty("the change has not committed yet");

        await transaction.CommitAsync();

        _sink.Reported.Should().Equal((_tenantId, contact.Id));
    }

    [Fact]
    public async Task ASaveInsideATransactionThatRollsBackIsNeverReported()
    {
        await using (var db = Context())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Contacts.Add(NewContact());
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();

            // A later save in the same context must not carry the abandoned change along.
            var kept = NewContact();
            db.Contacts.Add(kept);
            await db.SaveChangesAsync();

            _sink.Reported.Should().Equal((_tenantId, kept.Id));
        }
    }

    [Fact]
    public async Task ASaveOutsideATransactionIsReportedAtOnce()
    {
        await using var db = Context();
        var contact = NewContact();
        db.Contacts.Add(contact);

        await db.SaveChangesAsync();

        _sink.Reported.Should().Equal((_tenantId, contact.Id));
    }

    private Contact NewContact() => new()
    {
        Id = Guid.NewGuid(), TenantId = _tenantId, FirstName = "Ann", CreatedByUserId = _userId, IsActive = true,
    };

    private HomeManagementDbContext Context()
    {
        var tenant = new Mock<ITenantProvider>();
        tenant.SetupGet(t => t.TenantId).Returns(_tenantId);
        tenant.SetupGet(t => t.UserId).Returns(_userId);
        return new HomeManagementDbContext(
            new DbContextOptionsBuilder<HomeManagementDbContext>()
                .UseNpgsql(fixture.ConnectionString)
                .AddInterceptors(new ContactChangeInterceptor(_services))
                .Options,
            tenant.Object);
    }

    private sealed class RecordingSink : IContactChangeSink
    {
        public List<(Guid TenantId, Guid ContactId)> Reported { get; } = new();

        public Task ContactsChangedAsync(Guid tenantId, IReadOnlyCollection<Guid> contactIds, CancellationToken ct = default)
        {
            Reported.AddRange(contactIds.Select(id => (tenantId, id)));
            return Task.CompletedTask;
        }
    }
}
