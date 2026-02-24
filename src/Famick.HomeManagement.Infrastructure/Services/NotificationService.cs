using Famick.HomeManagement.Core.DTOs.Notifications;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly HomeManagementDbContext _db;
    private readonly ILogger<NotificationService> _logger;

    private static readonly Dictionary<NotificationType, string> DisplayNames = new()
    {
        { NotificationType.ExpiryLowStock, "Expiring / Low Stock Items" },
        { NotificationType.TaskSummary, "Pending Tasks" },
        { NotificationType.NewFeatures, "New Features" }
    };

    public NotificationService(
        HomeManagementDbContext db,
        ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    #region Notifications

    public async Task<NotificationListResponse> GetNotificationsAsync(
        Guid userId,
        NotificationListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Notifications
            .Where(n => n.UserId == userId && n.DismissedAt == null);

        if (request.ReadFilter.HasValue)
        {
            query = query.Where(n => n.IsRead == request.ReadFilter.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Type = n.Type,
                Title = n.Title,
                Summary = n.Summary,
                DeepLinkUrl = n.DeepLinkUrl,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new NotificationListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && n.DismissedAt == null)
            .CountAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null) return;

        notification.IsRead = true;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), cancellationToken);
    }

    public async Task DismissAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

        if (notification is null) return;

        notification.DismissedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateNotificationAsync(
        Guid userId,
        Guid tenantId,
        NotificationType type,
        string title,
        string summary,
        string? deepLinkUrl = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            TenantId = tenantId,
            Type = type,
            Title = title,
            Summary = summary,
            DeepLinkUrl = deepLinkUrl
        };

        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> WasNotifiedTodayAsync(
        Guid userId,
        NotificationType type,
        CancellationToken cancellationToken = default)
    {
        var todayUtc = DateTime.UtcNow.Date;
        return await _db.Notifications
            .AnyAsync(n => n.UserId == userId
                && n.Type == type
                && n.CreatedAt >= todayUtc,
                cancellationToken);
    }

    public async Task CleanupOldNotificationsAsync(
        int retentionDays,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var deleted = await _db.Notifications
            .Where(n => n.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("Cleaned up {Count} notifications older than {Days} days", deleted, retentionDays);
        }
    }

    #endregion

    #region Preferences

    public async Task<List<NotificationPreferenceDto>> GetPreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        var result = new List<NotificationPreferenceDto>();

        foreach (var type in Enum.GetValues<NotificationType>())
        {
            var pref = existing.FirstOrDefault(p => p.NotificationType == type);
            result.Add(new NotificationPreferenceDto
            {
                NotificationType = type,
                DisplayName = DisplayNames.GetValueOrDefault(type, type.ToString()),
                EmailEnabled = pref?.EmailEnabled ?? true,
                PushEnabled = pref?.PushEnabled ?? true,
                InAppEnabled = pref?.InAppEnabled ?? true
            });
        }

        return result;
    }

    public async Task UpdatePreferencesAsync(
        Guid userId,
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.NotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var dto in request.Preferences)
        {
            var pref = existing.FirstOrDefault(p => p.NotificationType == dto.NotificationType);
            if (pref is null)
            {
                pref = new NotificationPreference
                {
                    UserId = userId,
                    NotificationType = dto.NotificationType
                };
                _db.NotificationPreferences.Add(pref);
            }

            pref.EmailEnabled = dto.EmailEnabled;
            pref.PushEnabled = dto.PushEnabled;
            pref.InAppEnabled = dto.InAppEnabled;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DisableEmailForTypeAsync(
        Guid userId,
        Guid tenantId,
        NotificationType type,
        CancellationToken cancellationToken = default)
    {
        var pref = await _db.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.NotificationType == type, cancellationToken);

        if (pref is null)
        {
            pref = new NotificationPreference
            {
                UserId = userId,
                TenantId = tenantId,
                NotificationType = type,
                EmailEnabled = false,
                PushEnabled = true,
                InAppEnabled = true
            };
            _db.NotificationPreferences.Add(pref);
        }
        else
        {
            pref.EmailEnabled = false;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Email notifications disabled for user {UserId}, type {Type}", userId, type);
    }

    #endregion

    #region Device Tokens

    public async Task<DeviceTokenDto> RegisterDeviceTokenAsync(
        Guid userId,
        RegisterDeviceTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        // Remove any existing token with the same value (re-registration)
        var existingToken = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.Token == request.Token, cancellationToken);

        if (existingToken is not null)
        {
            existingToken.UserId = userId;
            existingToken.Platform = request.Platform;
            existingToken.LastUsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return new DeviceTokenDto
            {
                Id = existingToken.Id,
                Platform = existingToken.Platform,
                CreatedAt = existingToken.CreatedAt,
                LastUsedAt = existingToken.LastUsedAt
            };
        }

        var token = new UserDeviceToken
        {
            UserId = userId,
            Token = request.Token,
            Platform = request.Platform,
            LastUsedAt = DateTime.UtcNow
        };

        _db.UserDeviceTokens.Add(token);
        await _db.SaveChangesAsync(cancellationToken);

        return new DeviceTokenDto
        {
            Id = token.Id,
            Platform = token.Platform,
            CreatedAt = token.CreatedAt,
            LastUsedAt = token.LastUsedAt
        };
    }

    public async Task UnregisterDeviceTokenAsync(
        Guid userId,
        Guid tokenId,
        CancellationToken cancellationToken = default)
    {
        var token = await _db.UserDeviceTokens
            .FirstOrDefaultAsync(t => t.Id == tokenId && t.UserId == userId, cancellationToken);

        if (token is not null)
        {
            _db.UserDeviceTokens.Remove(token);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<UserDeviceToken>> GetDeviceTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _db.UserDeviceTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    #endregion
}
