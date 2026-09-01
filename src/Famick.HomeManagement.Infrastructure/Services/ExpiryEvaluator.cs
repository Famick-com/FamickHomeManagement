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
            // Two jars of the same thing, bought together, are two stock entries with the
            // same name, date and place — and read as the same line printed twice. Collapsed
            // into one line carrying a count, which is shorter and says more.
            .GroupBy(s => new
            {
                Name = s.Product!.Name,
                Date = s.BestBeforeDate!.Value.Date,
                Location = s.Location?.Name ?? "Unknown"
            })
            .Select(group => new ExpiryItemData
            {
                ProductName = group.Key.Name,
                ExpiryDate = group.Key.Date.ToString("yyyy-MM-dd"),
                LocationName = group.Key.Location,
                IsExpired = group.Key.Date < today,
                DaysUntilExpiry = (group.Key.Date - today).Days,
                Quantity = group.Count()
            })
            .OrderBy(x => x.ExpiryDate)
            .ToList();

        if (expiringItems.Count == 0)
            return Array.Empty<NotificationItem>();

        // Counts stay in stock entries rather than lines, so "144 items" still means 144
        // things in the cupboard even where two of them share a line.
        var expiredCount = expiringItems.Where(x => x.IsExpired).Sum(x => x.Quantity);
        var expiringSoonCount = expiringItems.Where(x => !x.IsExpired).Sum(x => x.Quantity);

        // The heading has to match what the list actually shows. It said "expiring soon"
        // whatever the contents, so a message reporting 144 already-expired items opened
        // by calling them all upcoming.
        var title = (expiredCount, expiringSoonCount) switch
        {
            ( > 0, 0) => $"{expiredCount} item(s) expired",
            (0, > 0) => $"{expiringSoonCount} item(s) expiring soon",
            _ => $"{expiredCount + expiringSoonCount} item(s) need attention"
        };

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
