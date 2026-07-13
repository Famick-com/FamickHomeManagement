using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Notifications;

/// <summary>
/// A single future-dated reminder returned by the "upcoming reminders" prefetch feed.
/// The mobile app fetches these while online (self-hosted mode) and hands each one to the
/// native OS scheduler (iOS <c>UNCalendarNotificationTrigger</c> / Android <c>AlarmManager</c>)
/// so it fires locally at <see cref="FireAtUtc"/> even with no network — the "download once,
/// alert anytime" strategy that replaces cloud push on self-hosted servers.
/// </summary>
/// <param name="Key">Stable dedup key so the client can diff against what it has already scheduled.</param>
/// <param name="Type">The notification type this reminder represents.</param>
/// <param name="FireAtUtc">When the local notification should fire (UTC).</param>
/// <param name="Title">Notification title.</param>
/// <param name="Body">Notification body.</param>
/// <param name="DeepLinkUrl">Optional deep link to open when the notification is tapped.</param>
/// <param name="ContentHash">Hash of the rendered content so the client can detect changes and reschedule.</param>
public record UpcomingReminderDto(
    string Key,
    MessageType Type,
    DateTime FireAtUtc,
    string Title,
    string Body,
    string? DeepLinkUrl,
    string ContentHash);
