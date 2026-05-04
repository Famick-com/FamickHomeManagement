using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Postgres-backed <see cref="IJwtMinIatService"/> implementation. Suitable for
/// self-hosted (no Redis dependency) and used as the inner store for the cloud
/// Redis-cached decorator.
/// </summary>
public class JwtMinIatService : IJwtMinIatService
{
    private readonly HomeManagementDbContext _context;
    private readonly ILogger<JwtMinIatService> _logger;

    public JwtMinIatService(HomeManagementDbContext context, ILogger<JwtMinIatService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<long> GetMinIatAsync(Guid userId, CancellationToken ct = default)
    {
        // Bypass tenant query filters — the middleware reads this from the JWT
        // long before the tenant context is established for the request.
        var row = await _context.UserJwtMinIats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        return row?.MinIat ?? 0L;
    }

    public async Task BumpAsync(Guid userId, long newMinIat, CancellationToken ct = default)
    {
        // Find existing row (bypassing tenant filters — same reason as GetMinIatAsync).
        var existing = await _context.UserJwtMinIats
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (existing is null)
        {
            // First-ever bump for this user. Need to look up the user's tenant
            // to populate the row's TenantId for the existing tenant query filter.
            var user = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.TenantId })
                .FirstOrDefaultAsync(ct);

            if (user is null)
            {
                _logger.LogWarning(
                    "BumpAsync called for unknown user {UserId} — ignoring", userId);
                return;
            }

            _context.UserJwtMinIats.Add(new UserJwtMinIat
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TenantId = user.TenantId,
                MinIat = newMinIat,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else if (newMinIat > existing.MinIat)
        {
            existing.MinIat = newMinIat;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // Backwards or equal — no-op. Don't even save.
            return;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation(
            "jwt_min_iat bumped for user {UserId} to {MinIat}", userId, newMinIat);
    }
}
