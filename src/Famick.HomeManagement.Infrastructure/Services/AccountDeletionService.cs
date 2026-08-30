using Famick.HomeManagement.Core.DTOs.Account;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Interfaces;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <inheritdoc />
public class AccountDeletionService : IAccountDeletionService
{
    /// <summary>
    /// How long a scheduled deletion can still be called off. The delay is the feature —
    /// it is what turns an irreversible tap into a recoverable one — so it is stated to
    /// the user rather than hidden.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(30);

    private readonly HomeManagementDbContext _context;
    private readonly IJwtMinIatService _jwtMinIat;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        HomeManagementDbContext context,
        IJwtMinIatService jwtMinIat,
        ILogger<AccountDeletionService> logger)
    {
        _context = context;
        _jwtMinIat = jwtMinIat;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AccountDeletionStatusDto> GetStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct);
        var tenant = await LoadTenantAsync(user.TenantId, ct);
        var isAdmin = await IsAdminAsync(user, ct);
        var otherMembers = await CountOtherMembersAsync(user.TenantId, userId, ct);

        var scope = isAdmin ? AccountDeletionScope.Household : AccountDeletionScope.User;

        // A pending household deletion outranks a pending user deletion in what it
        // describes: the member's own row is going either way, but the household taking
        // everything with it is the fact worth reporting.
        if (tenant.DeletionRequestedAt.HasValue)
        {
            return new AccountDeletionStatusDto
            {
                IsPending = true,
                Scope = AccountDeletionScope.Household,
                RequestedAt = tenant.DeletionRequestedAt,
                PurgeAfter = tenant.DeletionPurgeAfter,
                HouseholdName = tenant.Name,
                OtherMemberCount = otherMembers
            };
        }

        return new AccountDeletionStatusDto
        {
            IsPending = user.DeletionRequestedAt.HasValue,
            Scope = scope,
            RequestedAt = user.DeletionRequestedAt,
            PurgeAfter = user.DeletionPurgeAfter,
            HouseholdName = tenant.Name,
            OtherMemberCount = otherMembers
        };
    }

    /// <inheritdoc />
    public async Task<AccountDeletionRequestResultDto> RequestAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct);
        var tenant = await LoadTenantAsync(user.TenantId, ct);
        var isAdmin = await IsAdminAsync(user, ct);

        var now = DateTime.UtcNow;
        var purgeAfter = now + GracePeriod;
        var otherMembers = await CountOtherMembersAsync(user.TenantId, userId, ct);

        if (isAdmin)
        {
            tenant.DeletionRequestedAt = now;
            tenant.DeletionPurgeAfter = purgeAfter;
            tenant.DeletionRequestedByUserId = userId;
            tenant.UpdatedAt = now;

            await _context.SaveChangesAsync(ct);

            // Everyone in the household loses access now, not on purge day. Leaving them
            // working for a month and then deleting the data underneath them would be the
            // worse of the two surprises.
            var memberIds = await _context.Users
                .IgnoreQueryFilters()
                .Where(u => u.TenantId == tenant.Id)
                .Select(u => u.Id)
                .ToListAsync(ct);

            foreach (var memberId in memberIds)
                await EndSessionsAsync(memberId, ct);

            _logger.LogInformation(
                "Household deletion requested for tenant {TenantId} by {UserId}; purge after {PurgeAfter}; {MemberCount} member(s) affected",
                tenant.Id, userId, purgeAfter, memberIds.Count);

            return new AccountDeletionRequestResultDto
            {
                Scope = AccountDeletionScope.Household,
                RequestedAt = now,
                PurgeAfter = purgeAfter,
                OtherMembersAffected = otherMembers
            };
        }

        user.DeletionRequestedAt = now;
        user.DeletionPurgeAfter = purgeAfter;
        user.UpdatedAt = now;

        await _context.SaveChangesAsync(ct);
        await EndSessionsAsync(userId, ct);

        _logger.LogInformation(
            "Account deletion requested for user {UserId}; purge after {PurgeAfter}", userId, purgeAfter);

        return new AccountDeletionRequestResultDto
        {
            Scope = AccountDeletionScope.User,
            RequestedAt = now,
            PurgeAfter = purgeAfter,
            OtherMembersAffected = 0
        };
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct);
        var cancelled = false;

        if (user.DeletionRequestedAt.HasValue)
        {
            user.DeletionRequestedAt = null;
            user.DeletionPurgeAfter = null;
            user.UpdatedAt = DateTime.UtcNow;
            cancelled = true;
        }

        // Only an admin can call off a household deletion. A member returning should get
        // their own account back without quietly reversing a decision that was not theirs.
        if (await IsAdminAsync(user, ct))
        {
            var tenant = await LoadTenantAsync(user.TenantId, ct);
            if (tenant.DeletionRequestedAt.HasValue)
            {
                tenant.DeletionRequestedAt = null;
                tenant.DeletionPurgeAfter = null;
                tenant.DeletionRequestedByUserId = null;
                tenant.UpdatedAt = DateTime.UtcNow;
                cancelled = true;
            }
        }

        if (cancelled)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Pending deletion cancelled by user {UserId}", userId);
        }

        return cancelled;
    }

    /// <inheritdoc />
    public async Task<AccountPurgeSummary> PurgeDueAsync(DateTime asOfUtc, CancellationToken ct = default)
    {
        var households = 0;
        var users = 0;

        var dueTenantIds = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletionPurgeAfter != null && t.DeletionPurgeAfter <= asOfUtc)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var tenantId in dueTenantIds)
        {
            ct.ThrowIfCancellationRequested();
            await PurgeHouseholdAsync(tenantId, ct);
            households++;
        }

        // Users whose own deletion came due. Anyone in a household just purged is already
        // gone, so this only picks up members leaving a household that survives.
        var dueUserIds = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.DeletionPurgeAfter != null && u.DeletionPurgeAfter <= asOfUtc)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var userId in dueUserIds)
        {
            ct.ThrowIfCancellationRequested();
            await PurgeUserAsync(userId, ct);
            users++;
        }

        if (households > 0 || users > 0)
            _logger.LogInformation("Purged {Households} household(s) and {Users} user(s)", households, users);

        return new AccountPurgeSummary(users, households);
    }

    /// <inheritdoc />
    public async Task<AccountAccessDecision> ReconcileAuthenticatedRequestAsync(
        Guid userId, long tokenIssuedAtUnixSeconds, CancellationToken ct = default)
    {
        var state = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                UserRequestedAt = u.DeletionRequestedAt,
                TenantRequestedAt = _context.Tenants
                    .IgnoreQueryFilters()
                    .Where(t => t.Id == u.TenantId)
                    .Select(t => t.DeletionRequestedAt)
                    .FirstOrDefault(),
                IsAdmin = _context.UserRoles
                    .IgnoreQueryFilters()
                    .Any(r => r.UserId == u.Id && r.Role == Role.Admin)
            })
            .FirstOrDefaultAsync(ct);

        // Overwhelmingly the common case: nothing pending, one lookup, done.
        if (state == null || (state.UserRequestedAt == null && state.TenantRequestedAt == null))
            return AccountAccessDecision.Allow;

        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(tokenIssuedAtUnixSeconds).UtcDateTime;

        if (state.TenantRequestedAt is { } householdRequestedAt)
        {
            if (!state.IsAdmin)
                return AccountAccessDecision.HouseholdDeletionPending;

            if (issuedAt <= householdRequestedAt)
                return AccountAccessDecision.HouseholdDeletionPending;
        }
        else if (state.UserRequestedAt is { } userRequestedAt && issuedAt <= userRequestedAt)
        {
            // Token predates the request, so this is not a return. Nothing to cancel, but
            // nothing to refuse either — a member's own pending deletion does not lock
            // them out.
            return AccountAccessDecision.Allow;
        }

        await CancelAsync(userId, ct);
        return AccountAccessDecision.Allow;
    }

    #region Purge

    /// <summary>
    /// Removes a household and every row belonging to it.
    /// </summary>
    /// <remarks>
    /// Rows are deleted table by table in an order computed from the EF model, inside one
    /// transaction. The order has to put dependents before principals: 11 of the model's
    /// relationships are <c>Restrict</c>, and those refuse to cascade, so a wrong order
    /// fails at exactly the moment someone closes their household. Deriving it from the
    /// model rather than writing it down means a newly added entity is covered without
    /// anyone remembering to update a list — and
    /// <c>AccountDeletionServiceTests.DeleteOrderSatisfiesEveryModelForeignKey</c> fails
    /// the build if the model ever grows a cycle this cannot express.
    /// </remarks>
    private async Task PurgeHouseholdAsync(Guid tenantId, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        await NullOptionalReferencesAsync(tenantId, ct);

        foreach (var entityType in TenantEntityTypesInDeleteOrder(_context.Model))
        {
            ct.ThrowIfCancellationRequested();

            var table = entityType.GetSchemaQualifiedTableName();
            var tenantColumn = TenantColumnName(entityType);
            if (table == null || tenantColumn == null) continue;

            await _context.Database.ExecuteSqlRawAsync(
                $"DELETE FROM {Quote(entityType)} WHERE {Quote(tenantColumn)} = {{0}}",
                new object[] { tenantId }, ct);
        }

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM \"Tenants\" WHERE \"Id\" = {0}", new object[] { tenantId }, ct);

        await transaction.CommitAsync(ct);

        _logger.LogInformation("Household {TenantId} purged", tenantId);
    }

    /// <summary>
    /// Removes one member from a household that carries on without them.
    /// </summary>
    /// <remarks>
    /// Only what belongs to the person goes — their login, their sessions, their identity
    /// records. Anything they contributed to the household (products, events, inventory)
    /// belongs to the household and stays; deleting it would take data from the people
    /// still using it.
    /// </remarks>
    private async Task PurgeUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null) return;

        _context.RefreshTokens.RemoveRange(
            await _context.RefreshTokens.IgnoreQueryFilters().Where(t => t.UserId == userId).ToListAsync(ct));
        _context.UserRoles.RemoveRange(
            await _context.UserRoles.IgnoreQueryFilters().Where(r => r.UserId == userId).ToListAsync(ct));
        _context.UserPermissions.RemoveRange(
            await _context.UserPermissions.IgnoreQueryFilters().Where(p => p.UserId == userId).ToListAsync(ct));
        _context.UserExternalLogins.RemoveRange(
            await _context.UserExternalLogins.IgnoreQueryFilters().Where(l => l.UserId == userId).ToListAsync(ct));
        _context.UserPasskeyCredentials.RemoveRange(
            await _context.UserPasskeyCredentials.IgnoreQueryFilters().Where(c => c.UserId == userId).ToListAsync(ct));
        _context.UserJwtMinIats.RemoveRange(
            await _context.UserJwtMinIats.IgnoreQueryFilters().Where(m => m.UserId == userId).ToListAsync(ct));

        _context.Users.Remove(user);

        // One SaveChanges so EF orders the deletes and the whole removal is atomic.
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} purged", userId);
    }

    /// <summary>
    /// Orders the tenant-scoped entity types so that every type is deleted before the
    /// types it points at. Exposed for the test that checks the order against the model.
    /// </summary>
    /// <remarks>
    /// Only required relationships constrain the order. Optional ones are set to null
    /// first (see <see cref="NullOptionalReferencesAsync"/>), which is what makes an order
    /// possible at all: <c>User.ContactId</c> and <c>Contact.LinkedUserId</c> point at
    /// each other, so the graph genuinely has a cycle until the nullable edges are cut.
    /// A cycle among required relationships could not be ordered — but could not be
    /// inserted either, so it cannot arise.
    /// </remarks>
    public static IReadOnlyList<IEntityType> TenantEntityTypesInDeleteOrder(IModel model)
    {
        var tenantTypes = model.GetEntityTypes()
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetTableName() != null)
            .ToList();

        var inScope = new HashSet<IEntityType>(tenantTypes);
        var ordered = new List<IEntityType>();
        var visiting = new HashSet<IEntityType>();
        var visited = new HashSet<IEntityType>();

        void Visit(IEntityType type)
        {
            if (visited.Contains(type) || !visiting.Add(type)) return;

            // Everything that requires this type has to go first.
            foreach (var fk in type.GetReferencingForeignKeys())
            {
                var dependent = fk.DeclaringEntityType;
                if (dependent != type && fk.IsRequired && inScope.Contains(dependent))
                    Visit(dependent);
            }

            visiting.Remove(type);
            visited.Add(type);
            ordered.Add(type);
        }

        foreach (var type in tenantTypes) Visit(type);

        return ordered;
    }

    /// <summary>
    /// Clears every optional reference between this household's rows before any of them
    /// are deleted.
    /// </summary>
    /// <remarks>
    /// Two things need this. Cycles: <c>User</c> and <c>Contact</c> reference each other,
    /// so neither can go first while both links stand. And self-referencing hierarchies —
    /// <c>Contact.ParentContactId</c> — where a Restrict rule is checked per row and
    /// refuses a parent whose children are in the same statement.
    /// </remarks>
    private async Task NullOptionalReferencesAsync(Guid tenantId, CancellationToken ct)
    {
        var tenantTypes = _context.Model.GetEntityTypes()
            .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType))
            .Where(t => t.GetTableName() != null)
            .ToList();

        var inScope = new HashSet<IEntityType>(tenantTypes);

        foreach (var entityType in tenantTypes)
        {
            var tenantColumn = TenantColumnName(entityType);
            if (tenantColumn == null) continue;

            var columns = entityType.GetForeignKeys()
                .Where(fk => !fk.IsRequired)
                .Where(fk => inScope.Contains(fk.PrincipalEntityType))
                .SelectMany(fk => fk.Properties)
                .Where(p => p.IsNullable)
                .Select(p => ColumnName(entityType, p))
                .Where(c => c != null && c != tenantColumn)
                .Distinct()
                .ToList();

            if (columns.Count == 0) continue;

            var assignments = string.Join(", ", columns.Select(c => $"{Quote(c!)} = NULL"));

            await _context.Database.ExecuteSqlRawAsync(
                $"UPDATE {Quote(entityType)} SET {assignments} WHERE {Quote(tenantColumn)} = {{0}}",
                new object[] { tenantId }, ct);
        }
    }

    private static string? TenantColumnName(IEntityType entityType)
    {
        var property = entityType.FindProperty(nameof(ITenantEntity.TenantId));
        return property == null ? null : ColumnName(entityType, property);
    }

    private static string? ColumnName(IEntityType entityType, IProperty property)
    {
        var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
        return storeObject.HasValue
            ? property.GetColumnName(storeObject.Value)
            : property.GetColumnName();
    }

    private static string Quote(IEntityType entityType)
    {
        var schema = entityType.GetSchema();
        var table = entityType.GetTableName()!;
        return schema == null ? Quote(table) : $"{Quote(schema)}.{Quote(table)}";
    }

    private static string Quote(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    #endregion

    #region Helpers

    private async Task<User> LoadUserAsync(Guid userId, CancellationToken ct)
    {
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        return user ?? throw new EntityNotFoundException("User", userId);
    }

    private async Task<Tenant> LoadTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        return tenant ?? throw new EntityNotFoundException("Tenant", tenantId);
    }

    private async Task<bool> IsAdminAsync(User user, CancellationToken ct)
    {
        return await _context.UserRoles
            .IgnoreQueryFilters()
            .AnyAsync(r => r.UserId == user.Id && r.Role == Role.Admin, ct);
    }

    private async Task<int> CountOtherMembersAsync(Guid tenantId, Guid userId, CancellationToken ct)
    {
        return await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId && u.Id != userId, ct);
    }

    /// <summary>
    /// Revokes refresh tokens and invalidates outstanding access tokens. Without the
    /// second half, an access token issued moments before the request would still work
    /// for its remaining lifetime — and the cancel-on-return rule reads any authenticated
    /// request as a deliberate return, so a stale token would silently undo the deletion.
    /// </summary>
    private async Task EndSessionsAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // Loaded and modified rather than issued as a set-based update: a user holds a
        // handful of refresh tokens, so the round trip is cheap, and it keeps this path
        // exercisable by the unit tests that cover the admin/member split.
        var tokens = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }

        if (tokens.Count > 0)
            await _context.SaveChangesAsync(ct);

        await _jwtMinIat.BumpAsync(userId, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1, ct);
    }

    #endregion
}
