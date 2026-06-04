using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel;
using Famick.HomeManagement.Infrastructure.AuthProxy.Tunnel.Protocol;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class CloudLoginOptInService : ICloudLoginOptInService
{
    private readonly HomeManagementDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ITunnelSender _tunnelSender;
    private readonly ILogger<CloudLoginOptInService> _logger;

    public CloudLoginOptInService(
        HomeManagementDbContext db,
        ITenantProvider tenantProvider,
        ITunnelSender tunnelSender,
        ILogger<CloudLoginOptInService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _tunnelSender = tunnelSender;
        _logger = logger;
    }

    public async Task<bool> IsOptedInAsync(Guid userId, CancellationToken ct)
    {
        return await _db.UserCloudLoginOptIns
            .AsNoTracking()
            .AnyAsync(o => o.UserId == userId, ct)
            .ConfigureAwait(false);
    }

    public async Task OptInAsync(Guid userId, CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant context not set; cloud-login opt-in requires an authenticated user.");

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);
        if (user is null)
        {
            throw new InvalidOperationException($"User {userId} not found");
        }

        var existing = await _db.UserCloudLoginOptIns
            .FirstOrDefaultAsync(o => o.UserId == userId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _db.UserCloudLoginOptIns.Add(new UserCloudLoginOptIn
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                OptedInAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        // Push regardless of whether a row was created — replaying a
        // USER_REGISTER is idempotent on the server side and helps
        // recover from any local-vs-AuthProxy drift.
        var pushed = await _tunnelSender.TrySendAsync(new UserRegister(user.Email), ct).ConfigureAwait(false);
        _logger.LogInformation(
            "Cloud-login opt-in for user {UserId} ({Email}) — USER_REGISTER {Result}",
            userId, user.Email, pushed ? "sent" : "skipped (tunnel offline)");
    }

    public async Task OptOutAsync(Guid userId, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, ct)
            .ConfigureAwait(false);

        var existing = await _db.UserCloudLoginOptIns
            .FirstOrDefaultAsync(o => o.UserId == userId, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            _db.UserCloudLoginOptIns.Remove(existing);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        if (user is not null)
        {
            var pushed = await _tunnelSender.TrySendAsync(new UserUnregister(user.Email), ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Cloud-login opt-out for user {UserId} ({Email}) — USER_UNREGISTER {Result}",
                userId, user.Email, pushed ? "sent" : "skipped (tunnel offline)");
        }
    }

    public async Task<IReadOnlyList<string>> GetOptedInEmailsAsync(CancellationToken ct)
    {
        return await _db.UserCloudLoginOptIns
            .AsNoTracking()
            .Join(_db.Users.AsNoTracking(),
                o => o.UserId,
                u => u.Id,
                (o, u) => u.Email)
            .Where(email => email != null && email != string.Empty)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Guid>> GetOptedInUserIdsAsync(CancellationToken ct)
    {
        return await _db.UserCloudLoginOptIns
            .AsNoTracking()
            .Select(o => o.UserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
