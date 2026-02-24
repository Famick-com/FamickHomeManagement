using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Evaluates expiring stock entries and low-stock products for a tenant.
/// Produces one notification per user with a consolidated summary.
/// </summary>
public class ExpiryAndStockEvaluator : INotificationEvaluator
{
    private readonly HomeManagementDbContext _db;
    private readonly NotificationSettings _settings;
    private readonly ILogger<ExpiryAndStockEvaluator> _logger;

    public NotificationType Type => NotificationType.ExpiryLowStock;

    public ExpiryAndStockEvaluator(
        HomeManagementDbContext db,
        IOptions<NotificationSettings> settings,
        ILogger<ExpiryAndStockEvaluator> logger)
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

        // Find stock entries with BestBeforeDate within the warning threshold
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
            .Select(s => new
            {
                ProductName = s.Product!.Name,
                ExpiryDate = s.BestBeforeDate!.Value,
                LocationName = s.Location?.Name ?? "Unknown",
                IsExpired = s.BestBeforeDate.Value.Date < today
            })
            .OrderBy(x => x.ExpiryDate)
            .ToList();

        // Find products below minimum stock
        var lowStockProducts = await _db.Products
            .Where(p => p.TenantId == tenantId && p.IsActive && p.MinStockAmount > 0)
            .Select(p => new
            {
                p.Name,
                p.MinStockAmount,
                CurrentStock = _db.Stock
                    .Where(s => s.ProductId == p.Id && s.Amount > 0)
                    .Sum(s => s.Amount)
            })
            .Where(p => p.CurrentStock < p.MinStockAmount)
            .ToListAsync(cancellationToken);

        if (expiringItems.Count == 0 && lowStockProducts.Count == 0)
        {
            return Array.Empty<NotificationItem>();
        }

        // Build notification content
        var titleParts = new List<string>();
        if (expiringItems.Count > 0)
            titleParts.Add($"{expiringItems.Count} item(s) expiring soon");
        if (lowStockProducts.Count > 0)
            titleParts.Add($"{lowStockProducts.Count} item(s) low on stock");

        var title = string.Join(", ", titleParts);

        var summaryParts = new List<string>();
        if (expiringItems.Count > 0)
        {
            var expired = expiringItems.Count(x => x.IsExpired);
            var expiring = expiringItems.Count - expired;
            if (expired > 0) summaryParts.Add($"{expired} expired");
            if (expiring > 0) summaryParts.Add($"{expiring} expiring soon");
        }
        if (lowStockProducts.Count > 0)
        {
            summaryParts.Add($"{lowStockProducts.Count} below minimum stock");
        }
        var summary = string.Join("; ", summaryParts);

        // Build email body
        var emailHtml = BuildEmailHtml(expiringItems, lowStockProducts);
        var emailText = BuildEmailText(expiringItems, lowStockProducts);

        // Send to all users in the tenant
        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return users.Select(userId => new NotificationItem(
            userId,
            NotificationType.ExpiryLowStock,
            title,
            summary,
            "/stock",
            $"Famick: {title}",
            emailHtml,
            emailText
        )).ToList();
    }

    private static string BuildEmailHtml(
        IReadOnlyList<dynamic> expiringItems,
        IReadOnlyList<dynamic> lowStockProducts)
    {
        var html = "<h2>Inventory Alert</h2>";

        if (expiringItems.Count > 0)
        {
            html += "<h3>Items Expiring Soon</h3><table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;'>";
            html += "<tr><th>Product</th><th>Expiry Date</th><th>Location</th><th>Status</th></tr>";
            foreach (var item in expiringItems)
            {
                var status = (bool)item.IsExpired ? "<span style='color:red;'>Expired</span>" : "Expiring soon";
                html += $"<tr><td>{item.ProductName}</td><td>{((DateTime)item.ExpiryDate):yyyy-MM-dd}</td><td>{item.LocationName}</td><td>{status}</td></tr>";
            }
            html += "</table>";
        }

        if (lowStockProducts.Count > 0)
        {
            html += "<h3>Low Stock Items</h3><table border='1' cellpadding='8' cellspacing='0' style='border-collapse:collapse;'>";
            html += "<tr><th>Product</th><th>Current Amount</th><th>Minimum Amount</th></tr>";
            foreach (var item in lowStockProducts)
            {
                html += $"<tr><td>{item.Name}</td><td>{item.CurrentStock}</td><td>{item.MinStockAmount}</td></tr>";
            }
            html += "</table>";
        }

        return html;
    }

    private static string BuildEmailText(
        IReadOnlyList<dynamic> expiringItems,
        IReadOnlyList<dynamic> lowStockProducts)
    {
        var text = "INVENTORY ALERT\n\n";

        if (expiringItems.Count > 0)
        {
            text += "Items Expiring Soon:\n";
            foreach (var item in expiringItems)
            {
                var status = (bool)item.IsExpired ? "(EXPIRED)" : "(Expiring soon)";
                text += $"  - {item.ProductName} | Expiry: {((DateTime)item.ExpiryDate):yyyy-MM-dd} | Location: {item.LocationName} {status}\n";
            }
            text += "\n";
        }

        if (lowStockProducts.Count > 0)
        {
            text += "Low Stock Items:\n";
            foreach (var item in lowStockProducts)
            {
                text += $"  - {item.Name} | Current: {item.CurrentStock} | Minimum: {item.MinStockAmount}\n";
            }
        }

        return text;
    }
}
