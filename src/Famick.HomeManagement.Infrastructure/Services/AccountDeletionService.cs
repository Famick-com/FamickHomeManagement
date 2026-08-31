using System.Globalization;
using Famick.HomeManagement.Core.DTOs.Account;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Messaging.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// How long before the purge the final warning goes out.
    /// </summary>
    public static readonly TimeSpan ReminderLeadTime = TimeSpan.FromDays(3);

    private readonly HomeManagementDbContext _context;
    private readonly IJwtMinIatService _jwtMinIat;
    private readonly IEnumerable<IHouseholdPurgeParticipant> _purgeParticipants;
    private readonly IServiceProvider? _services;
    private readonly ILogger<AccountDeletionService> _logger;

    public AccountDeletionService(
        HomeManagementDbContext context,
        IJwtMinIatService jwtMinIat,
        ILogger<AccountDeletionService> logger,
        IEnumerable<IHouseholdPurgeParticipant>? purgeParticipants = null,
        IServiceProvider? services = null)
    {
        _context = context;
        _jwtMinIat = jwtMinIat;
        _logger = logger;
        _purgeParticipants = purgeParticipants ?? Array.Empty<IHouseholdPurgeParticipant>();
        _services = services;
    }

    /// <summary>
    /// Resolves the message service only when there is actually something to send.
    /// </summary>
    /// <remarks>
    /// Deliberately not a constructor dependency. <c>AccountDeletionMiddleware</c> resolves
    /// this service on every authenticated request, and ASP.NET constructs the parameter
    /// before the middleware body runs — so taking <see cref="IMessageService"/> directly
    /// would build the whole messaging graph (the service, all three transports, and their
    /// dependencies) on every request to the application, whether or not an email was ever
    /// going to be sent. Anything that throws while constructing any of it would then take
    /// down every request, including sign-in.
    /// </remarks>
    private IMessageService? Messages => _services?.GetService<IMessageService>();

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
            OtherMemberCount = otherMembers,
            CancelledNotice = BuildCancelledNotice(user)
        };
    }

    private static AccountDeletionCancelledNoticeDto? BuildCancelledNotice(User user)
    {
        if (user.DeletionCancelledNoticeAt is not { } cancelledAt) return null;

        return new AccountDeletionCancelledNoticeDto
        {
            CancelledAt = cancelledAt,
            RequestedAt = user.DeletionCancelledNoticeRequestedAt ?? cancelledAt,
            WasHousehold = user.DeletionCancelledNoticeWasHousehold
        };
    }

    /// <inheritdoc />
    public async Task AcknowledgeCancelledNoticeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await LoadUserAsync(userId, ct);

        if (user.DeletionCancelledNoticeAt == null) return;

        user.DeletionCancelledNoticeAt = null;
        user.DeletionCancelledNoticeRequestedAt = null;
        user.DeletionCancelledNoticeWasHousehold = false;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
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

            // Everyone losing access is told, not only the admin who asked. For the other
            // members this email is the only warning they get, and they cannot cancel it.
            await NotifyHouseholdAsync(
                tenant, MessageType.AccountDeletionScheduled, userId, now, purgeAfter, ct);

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

        await NotifyUserAsync(user, tenant, MessageType.AccountDeletionScheduled,
            isHousehold: false, isBystander: false, requestedAt: now, purgeAfter: purgeAfter, ct: ct);

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
        var tenant = await LoadTenantAsync(user.TenantId, ct);
        var now = DateTime.UtcNow;

        var cancelled = false;
        var wasHousehold = false;
        DateTime? originallyRequestedAt = null;

        if (user.DeletionRequestedAt.HasValue)
        {
            originallyRequestedAt = user.DeletionRequestedAt;
            user.DeletionRequestedAt = null;
            user.DeletionPurgeAfter = null;
            user.DeletionReminderSentAt = null;
            user.UpdatedAt = now;
            cancelled = true;
        }

        // Only an admin can call off a household deletion. A member returning should get
        // their own account back without quietly reversing a decision that was not theirs.
        if (await IsAdminAsync(user, ct) && tenant.DeletionRequestedAt.HasValue)
        {
            originallyRequestedAt = tenant.DeletionRequestedAt;
            wasHousehold = true;

            tenant.DeletionRequestedAt = null;
            tenant.DeletionPurgeAfter = null;
            tenant.DeletionRequestedByUserId = null;
            tenant.DeletionReminderSentAt = null;
            tenant.UpdatedAt = now;
            cancelled = true;
        }

        if (!cancelled) return false;

        // Held for the client to show at sign-in. Signing in is enough to cancel, so this
        // can happen without anyone deciding to — the person who meant it to go ahead has
        // to be told, or they find out weeks later when the data is still there.
        user.DeletionCancelledNoticeAt = now;
        user.DeletionCancelledNoticeRequestedAt = originallyRequestedAt;
        user.DeletionCancelledNoticeWasHousehold = wasHousehold;

        await _context.SaveChangesAsync(ct);

        if (wasHousehold)
        {
            await NotifyHouseholdAsync(
                tenant, MessageType.AccountDeletionCancelled,
                requestedByUserId: userId,
                requestedAt: originallyRequestedAt ?? now,
                purgeAfter: null, ct: ct);
        }
        else
        {
            await NotifyUserAsync(user, tenant, MessageType.AccountDeletionCancelled,
                isHousehold: false, isBystander: false,
                requestedAt: originallyRequestedAt ?? now, purgeAfter: null, ct: ct);
        }

        _logger.LogInformation("Pending deletion cancelled by user {UserId}", userId);

        return true;
    }

    /// <inheritdoc />
    public async Task<int> SendDueRemindersAsync(DateTime asOfUtc, CancellationToken ct = default)
    {
        var threshold = asOfUtc + ReminderLeadTime;
        var sent = 0;

        // Households first. A member of a household being closed also has their own
        // account going, but the household is the fact that matters and the one message
        // they should get.
        var tenants = await _context.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.DeletionPurgeAfter != null
                        && t.DeletionPurgeAfter <= threshold
                        && t.DeletionReminderSentAt == null)
            .ToListAsync(ct);

        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();

            await NotifyHouseholdAsync(
                tenant, MessageType.AccountDeletionReminder,
                requestedByUserId: tenant.DeletionRequestedByUserId,
                requestedAt: tenant.DeletionRequestedAt ?? asOfUtc,
                purgeAfter: tenant.DeletionPurgeAfter, ct: ct);

            tenant.DeletionReminderSentAt = asOfUtc;
            sent++;
        }

        var householdIds = tenants.Select(t => t.Id).ToHashSet();

        var users = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.DeletionPurgeAfter != null
                        && u.DeletionPurgeAfter <= threshold
                        && u.DeletionReminderSentAt == null)
            .ToListAsync(ct);

        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();

            // Already covered by their household's reminder — two emails saying the same
            // thing is worse than one.
            if (householdIds.Contains(user.TenantId)) continue;

            var tenant = await LoadTenantAsync(user.TenantId, ct);

            await NotifyUserAsync(user, tenant, MessageType.AccountDeletionReminder,
                isHousehold: false, isBystander: false,
                requestedAt: user.DeletionRequestedAt ?? asOfUtc,
                purgeAfter: user.DeletionPurgeAfter, ct: ct);

            user.DeletionReminderSentAt = asOfUtc;
            sent++;
        }

        if (sent > 0)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Sent {Count} deletion reminder(s)", sent);
        }

        return sent;
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
        // Everyone who needs telling, and what to tell them, has to be read before the
        // rows go. After the delete there is no record left to address an email from.
        var tenant = await LoadTenantAsync(tenantId, ct);
        var farewells = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId)
            .Select(u => new Farewell(
                u.Email,
                u.FirstName,
                true,
                tenant.Name,
                u.Id != tenant.DeletionRequestedByUserId,
                tenant.DeletionRequestedAt))
            .ToListAsync(ct);

        // Anything outside the database that needs cleaning up has to read the household
        // before it goes. Nothing destructive happens here — the delete below can still
        // roll back, and a cancelled subscription cannot.
        await PrepareParticipantsAsync(tenantId, ct);

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

        await CompleteParticipantsAsync(tenantId, ct);
        await SendFarewellsAsync(farewells, ct);
    }

    /// <summary>
    /// Sends the confirmation that the data is gone, addressed from details read before
    /// the delete. Sent afterwards, so it is never a promise that turns out to be untrue.
    /// </summary>
    private async Task SendFarewellsAsync(IEnumerable<Farewell> farewells, CancellationToken ct)
    {
        foreach (var farewell in farewells)
        {
            await SendAsync(MessageType.AccountDeleted, farewell.Email, BuildData(
                farewell.FirstName,
                farewell.HouseholdName,
                farewell.IsHousehold,
                farewell.IsBystander,
                farewell.RequestedAt ?? DateTime.UtcNow,
                purgeAfter: null), ct);
        }
    }

    /// <summary>
    /// Everything needed to write to someone after their record has been deleted.
    /// </summary>
    private sealed record Farewell(
        string Email,
        string? FirstName,
        bool IsHousehold,
        string? HouseholdName,
        bool IsBystander,
        DateTime? RequestedAt);

    private async Task PrepareParticipantsAsync(Guid tenantId, CancellationToken ct)
    {
        foreach (var participant in _purgeParticipants)
        {
            try
            {
                await participant.PrepareAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                // A participant that cannot read what it needs will not be able to clean
                // up, but that is not a reason to keep a household the user asked us to
                // destroy. Record it and carry on.
                _logger.LogError(ex,
                    "Purge participant {Participant} failed to prepare for household {TenantId}; " +
                    "its external cleanup may be incomplete",
                    participant.GetType().Name, tenantId);
            }
        }
    }

    private async Task CompleteParticipantsAsync(Guid tenantId, CancellationToken ct)
    {
        foreach (var participant in _purgeParticipants)
        {
            try
            {
                await participant.CompleteAsync(tenantId, ct);
            }
            catch (Exception ex)
            {
                // The household is already gone, so there is nothing to undo. What matters
                // is that the leftover is named in the log — nobody can find it from the
                // database any more.
                _logger.LogError(ex,
                    "Purge participant {Participant} failed for household {TenantId}; " +
                    "external resources may need clearing by hand",
                    participant.GetType().Name, tenantId);
            }
        }
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

        var tenant = await LoadTenantAsync(user.TenantId, ct);
        var farewell = new Farewell(
            user.Email, user.FirstName, false, tenant.Name, false, user.DeletionRequestedAt);

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

        await SendFarewellsAsync(new[] { farewell }, ct);
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

    #region Notifications

    /// <summary>
    /// Emails every member of a household.
    /// </summary>
    /// <remarks>
    /// Anyone who is not <paramref name="requestedByUserId"/> is a bystander: they did not
    /// ask for this and cannot call it off, so the templates must not tell them to sign in
    /// and all will be well.
    /// </remarks>
    private async Task NotifyHouseholdAsync(
        Tenant tenant,
        MessageType type,
        Guid? requestedByUserId,
        DateTime requestedAt,
        DateTime? purgeAfter,
        CancellationToken ct)
    {
        if (Messages == null) return;

        var members = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TenantId == tenant.Id)
            .ToListAsync(ct);

        foreach (var member in members)
        {
            await NotifyUserAsync(member, tenant, type,
                isHousehold: true,
                isBystander: member.Id != requestedByUserId,
                requestedAt: requestedAt,
                purgeAfter: purgeAfter,
                ct: ct);
        }
    }

    private Task NotifyUserAsync(
        User user,
        Tenant tenant,
        MessageType type,
        bool isHousehold,
        bool isBystander,
        DateTime requestedAt,
        DateTime? purgeAfter,
        CancellationToken ct)
    {
        return SendAsync(type, user.Email, BuildData(
            user.FirstName, tenant.Name, isHousehold, isBystander, requestedAt, purgeAfter), ct);
    }

    private static AccountDeletionData BuildData(
        string? firstName,
        string? householdName,
        bool isHousehold,
        bool isBystander,
        DateTime requestedAt,
        DateTime? purgeAfter)
    {
        var daysRemaining = purgeAfter.HasValue
            ? Math.Max(0, (int)Math.Ceiling((purgeAfter.Value - DateTime.UtcNow).TotalDays))
            : 0;

        return new AccountDeletionData
        {
            UserName = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName,
            IsHousehold = isHousehold,
            HouseholdName = householdName ?? string.Empty,
            IsBystander = isBystander,
            RequestedOn = FormatDate(requestedAt),
            DeletedOn = purgeAfter.HasValue ? FormatDate(purgeAfter.Value) : string.Empty,
            DaysRemaining = daysRemaining
        };
    }

    /// <summary>
    /// Invariant culture with the month spelled out. These emails name the day someone's
    /// data dies, and a numeric date is read differently on either side of the Atlantic.
    /// </summary>
    private static string FormatDate(DateTime value)
        => value.ToUniversalTime().ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Sends one email, treating failure as non-fatal.
    /// </summary>
    /// <remarks>
    /// None of these messages is worth failing the operation for. Refusing a deletion
    /// because its confirmation email bounced would leave the user unable to delete their
    /// account at all, and aborting a purge midway would be worse still.
    /// </remarks>
    private async Task SendAsync(MessageType type, string email, AccountDeletionData data, CancellationToken ct)
    {
        var messages = Messages;
        if (messages == null || string.IsNullOrWhiteSpace(email)) return;

        try
        {
            await messages.SendTransactionalAsync(email, type, data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send {MessageType} email", type);
        }
    }

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
