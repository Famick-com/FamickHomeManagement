using System.Data.Common;
using System.Runtime.CompilerServices;
using Famick.HomeManagement.Core.Helpers;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Data;

/// <summary>
/// Tells <see cref="IContactChangeSink"/> about every contact a save changed, once the change has
/// committed.
/// </summary>
/// <remarks>
/// <para>
/// An interceptor rather than calls in each service, because contacts are written from many
/// places (the contact and dietary services, the setup wizard, a member leaving, the cloud's own
/// seeding) and a writer that forgot to call would leave copies elsewhere silently stale.
/// </para>
/// <para>
/// A save outside a transaction is reported as soon as it succeeds. A save inside one is held
/// until the transaction commits, and dropped if it rolls back, so the sink never hears about a
/// change that did not happen. Bulk <c>ExecuteUpdate</c>/<c>ExecuteDelete</c> bypass the change
/// tracker and are not seen.
/// </para>
/// <para>
/// One instance serves every context created from the same options (a scope's contexts and its
/// factory-made ones), so what is collected is kept per context.
/// </para>
/// </remarks>
public sealed class ContactChangeInterceptor : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private readonly IServiceProvider _services;
    private readonly ConditionalWeakTable<DbContext, PendingChanges> _pending = new();

    public ContactChangeInterceptor(IServiceProvider services)
    {
        _services = services;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SavedAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await SavedAsync(eventData.Context, cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Forget(eventData.Context, saved: false);

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context, saved: false);
        return Task.CompletedTask;
    }

    public void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        DispatchAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();

    public Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) =>
        DispatchAsync(eventData.Context, cancellationToken);

    public void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Forget(eventData.Context, saved: true);

    public Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context, saved: true);
        return Task.CompletedTask;
    }

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData) =>
        Forget(eventData.Context, saved: true);

    public Task TransactionFailedAsync(
        DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Forget(eventData.Context, saved: true);
        return Task.CompletedTask;
    }

    private void Collect(DbContext? context)
    {
        if (context == null) return;

        var pending = _pending.GetOrCreateValue(context);
        var contacts = context.ChangeTracker.Entries<Contact>().ToDictionary(e => e.Entity.Id, e => e.Entity.TenantId);

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            switch (entry.Entity)
            {
                case Contact contact:
                    pending.Saving.Add((contact.TenantId, contact.Id));
                    break;
                case ContactAuditLog:
                    break;
                case var child when ContactOwnedFields.ChildTypes.Contains(child.GetType()):
                    var contactId = (Guid)entry.Property(nameof(ContactPhoneNumber.ContactId)).CurrentValue!;
                    pending.Saving.Add((TenantOf(entry, contactId, contacts), contactId));
                    break;
            }
        }
    }

    /// <summary>
    /// The household a child row belongs to: its own tenant column when it has one, otherwise
    /// the contact's when that is tracked. <see cref="Guid.Empty"/> when neither is known, which
    /// <see cref="DispatchAsync"/> resolves from the database.
    /// </summary>
    private static Guid TenantOf(EntityEntry entry, Guid contactId, IReadOnlyDictionary<Guid, Guid> contacts) =>
        entry.Entity is BaseTenantEntity tenantEntity ? tenantEntity.TenantId
        : contacts.TryGetValue(contactId, out var tenantId) ? tenantId
        : Guid.Empty;

    private async Task SavedAsync(DbContext? context, CancellationToken ct)
    {
        if (context == null || !_pending.TryGetValue(context, out var pending)) return;

        pending.Committing.UnionWith(pending.Saving);
        pending.Saving.Clear();

        if (context.Database.CurrentTransaction == null)
            await DispatchAsync(context, ct);
    }

    private void Forget(DbContext? context, bool saved)
    {
        if (context == null || !_pending.TryGetValue(context, out var pending)) return;

        pending.Saving.Clear();
        if (saved) pending.Committing.Clear();
    }

    private async Task DispatchAsync(DbContext? context, CancellationToken ct)
    {
        if (context == null || !_pending.TryGetValue(context, out var pending) || pending.Committing.Count == 0) return;

        // Taken before calling out, so a sink that saves through this context starts afresh.
        var changes = pending.Committing.ToList();
        pending.Committing.Clear();

        var sink = _services.GetService<IContactChangeSink>();
        if (sink == null) return;

        try
        {
            var unknown = changes.Where(c => c.TenantId == Guid.Empty).Select(c => c.ContactId).Distinct().ToList();
            var tenants = unknown.Count == 0
                ? new Dictionary<Guid, Guid>()
                : await context.Set<Contact>().IgnoreQueryFilters()
                    .Where(c => unknown.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id, c => c.TenantId, ct);

            var byTenant = changes
                .Select(c => c.TenantId != Guid.Empty ? c
                    : tenants.TryGetValue(c.ContactId, out var tenantId) ? (TenantId: tenantId, c.ContactId)
                    : c)
                .Where(c => c.TenantId != Guid.Empty)
                .GroupBy(c => c.TenantId, c => c.ContactId);

            foreach (var group in byTenant)
                await sink.ContactsChangedAsync(group.Key, group.Distinct().ToList(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The change has committed; failing the request now would report a save that worked
            // as one that did not.
            _services.GetService<ILogger<ContactChangeInterceptor>>()?.LogError(ex,
                "Contact change sink failed for {Count} contact(s); copies elsewhere may be stale", changes.Count);
        }
    }

    private sealed class PendingChanges
    {
        /// <summary>Collected while a save is in flight.</summary>
        public HashSet<(Guid TenantId, Guid ContactId)> Saving { get; } = new();

        /// <summary>Saved, waiting for the transaction around them to commit.</summary>
        public HashSet<(Guid TenantId, Guid ContactId)> Committing { get; } = new();
    }
}
