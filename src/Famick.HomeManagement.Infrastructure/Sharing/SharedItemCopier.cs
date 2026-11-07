using Famick.HomeManagement.Core.DTOs.Sharing;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Infrastructure.Data.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ContactConfiguration = Famick.HomeManagement.Infrastructure.Configuration.ContactConfiguration;
using ProductConfiguration = Famick.HomeManagement.Infrastructure.Data.Configurations.ProductConfiguration;

namespace Famick.HomeManagement.Infrastructure.Sharing;

/// <summary>
/// Copies a shared item from one household into another. Each half runs in a scope of its own, entered
/// for its household and member and checked to be so before anything is read or written:
/// <list type="bullet">
/// <item>the read, as the member who shared the item, in their household;</item>
/// <item>the write, as the recipient, in theirs, in one transaction, with any file it saved removed if
/// the transaction doesn't commit.</item>
/// </list>
/// Neither runs in the calling request's context, so setting a scope's household can't reach the
/// request's. A query with no household reads every household's rows, so a scope that isn't entered
/// stops the copy rather than running without one.
/// </summary>
public class SharedItemCopier : ISharedItemCopier
{
    // Each copy holds its pictures in memory until it is written; a few at a time bounds that.
    private static readonly SemaphoreSlim Concurrent = new(4, 4);

    private static readonly HashSet<string> OriginIndexes =
    [
        RecipeConfiguration.OriginIndexName,
        ProductConfiguration.OriginIndexName,
        MealConfiguration.OriginIndexName,
        ContactConfiguration.OriginIndexName
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMultiTenancyOptions _multiTenancy;
    private readonly ILogger<SharedItemCopier> _logger;

    public SharedItemCopier(IServiceScopeFactory scopeFactory, IMultiTenancyOptions multiTenancy, ILogger<SharedItemCopier> logger)
    {
        _scopeFactory = scopeFactory;
        _multiTenancy = multiTenancy;
        _logger = logger;
    }

    public async Task<SharedItemPreview?> PreviewAsync(SharedItemRef item, CancellationToken ct = default)
    {
        EnsureMultiTenant();
        return await InHouseholdAsync(item.OwnerTenantId, item.OwnerUserId, async (services, db) =>
        {
            var shared = await ReadAsync(services, db, item, maxImages: 1, ct);
            return shared == null
                ? null
                : Handler(services, item.Kind).Preview(shared.Snapshot, shared.HouseholdName, shared.Snapshot.PreviewImageKey != null);
        }, ct);
    }

    public async Task<SharedItemImage?> ReadPreviewImageAsync(SharedItemRef item, CancellationToken ct = default)
    {
        EnsureMultiTenant();
        return await InHouseholdAsync(item.OwnerTenantId, item.OwnerUserId, async (services, db) =>
        {
            var shared = await ReadAsync(services, db, item, maxImages: 1, ct);
            return shared?.Snapshot.PreviewImageKey is { } key && shared.Context.Images.TryGetValue(key, out var image)
                ? new SharedItemImage(image.ContentType, image.Bytes)
                : null;
        }, ct);
    }

    public async Task<SharedItemCopyResult?> CopyAsync(ShareCopyRequest request, CancellationToken ct = default)
    {
        EnsureMultiTenant();
        var item = request.Item;

        await Concurrent.WaitAsync(ct);
        try
        {
            var shared = await InHouseholdAsync(item.OwnerTenantId, item.OwnerUserId,
                (services, db) => ReadAsync(services, db, item, ShareLimits.MaxImages, ct), ct);
            if (shared == null)
                return null;

            var origin = new ShareOrigin(item.OwnerTenantId, request.RecipientTenantId, shared.HouseholdName);
            try
            {
                return await InHouseholdAsync(request.RecipientTenantId, request.RecipientUserId,
                    (services, db) => WriteAsync(services, db, item.Kind, shared, origin, request.RecipientTenantId, ct), ct);
            }
            catch (DbUpdateException ex) when (IsOriginRace(ex))
            {
                // The same item was added by another request in between; this one finds that copy.
                _logger.LogInformation("Shared {Kind} added concurrently; returning the existing copy", item.Kind);
                return await InHouseholdAsync(request.RecipientTenantId, request.RecipientUserId,
                    (services, db) => WriteAsync(services, db, item.Kind, shared, origin, request.RecipientTenantId, ct), ct);
            }
        }
        finally
        {
            Concurrent.Release();
        }
    }

    private sealed record SharedRead(SharedItemSnapshot Snapshot, ShareSnapshotContext Context, string HouseholdName);

    /// <summary>The item as its sharer sees it, or null when it isn't theirs to share any more.</summary>
    private static async Task<SharedRead?> ReadAsync(
        IServiceProvider services, HomeManagementDbContext db, SharedItemRef item, int maxImages, CancellationToken ct)
    {
        // A member who has left, or been removed, no longer shares anything.
        if (!await db.Users.AnyAsync(u => u.Id == item.OwnerUserId && u.TenantId == item.OwnerTenantId && u.IsActive, ct))
            return null;

        var context = new ShareSnapshotContext(maxImages);
        var snapshot = await Handler(services, item.Kind).SnapshotAsync(item.ItemId, context, ct);
        if (snapshot == null)
            return null;

        var householdName = await db.Tenants
            .Where(t => t.Id == item.OwnerTenantId)
            .Select(t => t.Name)
            .FirstOrDefaultAsync(ct);
        return new SharedRead(snapshot, context, ShareText.HouseholdName(householdName));
    }

    private async Task<SharedItemCopyResult> WriteAsync(
        IServiceProvider services, HomeManagementDbContext db, ShareableKind kind, SharedRead shared,
        ShareOrigin origin, Guid recipientTenantId, CancellationToken ct)
    {
        var import = new ShareImportContext(shared.Context, origin);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var copy = await Handler(services, kind).ImportAsync(shared.Snapshot, import, ct);

            // Everything this copy touched is the recipient's; anything else is a mistake to undo.
            var stray = db.ChangeTracker.Entries<ITenantEntity>().FirstOrDefault(e => e.Entity.TenantId != recipientTenantId);
            if (stray != null)
                throw new InvalidOperationException($"A shared {kind} copy touched a {stray.Metadata.ClrType.Name} outside the household");

            await transaction.CommitAsync(ct);
            return new SharedItemCopyResult
            {
                Kind = kind,
                ItemId = copy.Id,
                Name = copy.Name,
                AlreadyHad = copy.AlreadyHad,
                SkippedImages = copy.AlreadyHad ? 0 : shared.Context.SkippedImages
            };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            foreach (var remove in import.SavedFiles)
            {
                try
                {
                    await remove();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Couldn't remove a file saved by a shared {Kind} copy that didn't go through", kind);
                }
            }
            throw;
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> in a new scope entered for the household and member, away from the
    /// calling request: flow is suppressed when the work starts, so nothing of the request's context,
    /// its <c>HttpContext</c> included, reaches the scope.
    /// </summary>
    private async Task<T> InHouseholdAsync<T>(
        Guid tenantId, Guid userId, Func<IServiceProvider, HomeManagementDbContext, Task<T>> work, CancellationToken ct)
    {
        Task<T> task;
        using (ExecutionContext.SuppressFlow())
        {
            task = Task.Run(async () =>
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var services = scope.ServiceProvider;

                var tenantProvider = services.GetRequiredService<ITenantProvider>();
                tenantProvider.SetTenantId(tenantId);
                tenantProvider.SetUserId(userId);
                var db = services.GetRequiredService<HomeManagementDbContext>();

                if (tenantProvider.TenantId != tenantId || tenantProvider.UserId != userId || db.EffectiveTenantId != tenantId)
                    throw new InvalidOperationException("Couldn't enter the household to share an item");
                if (services.GetService<IHttpContextAccessor>()?.HttpContext != null)
                    throw new InvalidOperationException("A shared item copy ran in a request's context");

                return await work(services, db);
            }, ct);
        }

        return await task;
    }

    private void EnsureMultiTenant()
    {
        if (!_multiTenancy.IsMultiTenantEnabled || _multiTenancy.FixedTenantId != null)
            throw new InvalidOperationException("Sharing between households needs a server with more than one household");
    }

    private static IShareableItemHandler Handler(IServiceProvider services, ShareableKind kind) =>
        services.GetServices<IShareableItemHandler>().SingleOrDefault(h => h.Kind == kind)
        ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "Nothing shares that kind of item");

    private static bool IsOriginRace(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && postgres.ConstraintName != null
        && OriginIndexes.Contains(postgres.ConstraintName);
}
