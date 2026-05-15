using System.Text.Json;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class UserAuditLogger : IUserAuditLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HomeManagementDbContext _db;
    private readonly ILogger<UserAuditLogger> _logger;

    public UserAuditLogger(HomeManagementDbContext db, ILogger<UserAuditLogger> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid userId,
        Guid tenantId,
        UserAuditAction action,
        object? oldValues,
        object? newValues,
        string? description,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var entry = new UserAuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Action = action,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
            Description = description,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
        };

        _db.UserAuditLogs.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "UserAuditLog written: userId={UserId} action={Action}",
            userId,
            action);
    }
}
