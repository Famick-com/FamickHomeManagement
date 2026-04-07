using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Evaluates expiring stock entries for a tenant.
/// Produces one notification per user if there are any expiring items.
/// </summary>
public class ExpiryEvaluator : INotificationEvaluator
{
    private readonly HomeManagementDbContext _db;
    private readonly NotificationSettings _settings;
    private readonly ILogger<ExpiryEvaluator> _logger;

    public MessageType Type => MessageType.Expiry;

    public ExpiryEvaluator(
        HomeManagementDbContext db,
        IOptions<NotificationSettings> settings,
        ILogger<ExpiryEvaluator> logger)
    {
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationItem>> EvaluateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var defaultWarningDays = _settings.DefaultExpiryWarningDays;

        var expiringEntries = await _db.Stock
            .Include(s => s.Product)
            .Include(s => s.Location)
            .Where(s => s.TenantId == tenantId
                && s.Product != null
                && s.Product.IsActive
                && s.BestBeforeDate != null
                && s.Amount > 0)
            .ToListAsync(cancellationToken);

        var expiringItems = expiringEntries
            .Where(s =>
            {
                var warningDays = s.Product!.ExpiryWarningDays ?? defaultWarningDays;
                var warningDate = today.AddDays(warningDays);
                return s.BestBeforeDate!.Value.Date <= warningDate;
            })
            .Select(s => new ExpiryItemData
            {
                ProductName = s.Product!.Name,
                ExpiryDate = s.BestBeforeDate!.Value.ToString("yyyy-MM-dd"),
                LocationName = s.Location?.Name ?? "Unknown",
                IsExpired = s.BestBeforeDate.Value.Date < today
            })
            .OrderBy(x => x.ExpiryDate)
            .ToList();

        if (expiringItems.Count == 0)
            return Array.Empty<NotificationItem>();

        var expiredCount = expiringItems.Count(x => x.IsExpired);
        var expiringSoonCount = expiringItems.Count - expiredCount;

        var title = $"{expiringItems.Count} item(s) expiring soon";
        var summaryParts = new List<string>();
        if (expiredCount > 0) summaryParts.Add($"{expiredCount} expired");
        if (expiringSoonCount > 0) summaryParts.Add($"{expiringSoonCount} expiring soon");
        var summary = string.Join("; ", summaryParts);

        var data = new ExpiryData
        {
            Title = title,
            Summary = summary,
            DeepLinkUrl = "/stock",
            ExpiredCount = expiredCount,
            ExpiringSoonCount = expiringSoonCount,
            ExpiringItems = expiringItems
        };

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return users.Select(userId => new NotificationItem(
            userId,
            MessageType.Expiry,
            title,
            summary,
            "/stock",
            data
        )).ToList();
    }
}
