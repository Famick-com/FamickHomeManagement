using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Famick.HomeManagement.Shared.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class LocalServerResolver : ILocalServerResolver
{
    private const string PublicUrlConfigKey = "MobileAppSetup:PublicUrl";

    private readonly HomeManagementDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IMultiTenancyOptions _multiTenancyOptions;
    private readonly IUserAuditLogger _auditLogger;
    private readonly ILogger<LocalServerResolver> _logger;

    public LocalServerResolver(
        HomeManagementDbContext db,
        IConfiguration configuration,
        IMultiTenancyOptions multiTenancyOptions,
        IUserAuditLogger auditLogger,
        ILogger<LocalServerResolver> logger)
    {
        _db = db;
        _configuration = configuration;
        _multiTenancyOptions = multiTenancyOptions;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<string?> ResolveAndAuditAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        // Cloud accounts have no local server — Phase 4 hard-coded; Phase 8
        // may light this up via a Tenant column once proxy customers exist.
        if (_multiTenancyOptions.IsMultiTenantEnabled)
            return null;

        var configured = _configuration[PublicUrlConfigKey];
        if (string.IsNullOrWhiteSpace(configured))
            return null;

        var canonical = UrlCanonicalizer.CanonicalizeOrNull(configured);
        if (canonical is null)
        {
            _logger.LogWarning(
                "MobileAppSetup:PublicUrl ({Value}) is not a valid canonicalizable URL; LocalServer omitted",
                configured);
            return null;
        }

        var stored = user.LastDeliveredLocalServer;

        // No change → no DB write, no audit.
        if (string.Equals(stored, canonical, StringComparison.Ordinal))
            return canonical;

        // Audit only on genuine change (first delivery is silent).
        if (!string.IsNullOrEmpty(stored))
        {
            await _auditLogger.LogAsync(
                userId: user.Id,
                tenantId: user.TenantId,
                action: UserAuditAction.LocalServerChanged,
                oldValues: new { localServer = stored },
                newValues: new { localServer = canonical },
                description: "MobileAppSetup:PublicUrl change observed at login",
                ipAddress: ipAddress,
                userAgent: userAgent,
                cancellationToken: cancellationToken);
        }

        user.LastDeliveredLocalServer = canonical;
        await _db.SaveChangesAsync(cancellationToken);

        return canonical;
    }
}
