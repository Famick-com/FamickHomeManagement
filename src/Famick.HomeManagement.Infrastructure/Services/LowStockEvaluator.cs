using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Evaluates products below minimum stock level for a tenant.
/// Produces one notification per user if there are any low-stock items.
/// </summary>
public class LowStockEvaluator : INotificationEvaluator
{
    private readonly HomeManagementDbContext _db;
    private readonly ILogger<LowStockEvaluator> _logger;

    public MessageType Type => MessageType.LowStock;

    public LowStockEvaluator(
        HomeManagementDbContext db,
        ILogger<LowStockEvaluator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<NotificationItem>> EvaluateAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
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

        if (lowStockProducts.Count == 0)
            return Array.Empty<NotificationItem>();

        var title = $"{lowStockProducts.Count} item(s) low on stock";
        var summary = $"{lowStockProducts.Count} below minimum stock";

        var data = new LowStockData
        {
            Title = title,
            Summary = summary,
            DeepLinkUrl = "/stock",
            ItemCount = lowStockProducts.Count,
            LowStockItems = lowStockProducts.Select(p => new LowStockItemData
            {
                Name = p.Name,
                CurrentStock = p.CurrentStock,
                MinStockAmount = p.MinStockAmount
            }).ToList()
        };

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        return users.Select(userId => new NotificationItem(
            userId,
            MessageType.LowStock,
            title,
            summary,
            "/stock",
            data
        )).ToList();
    }
}
