using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Generic audit log for user-scoped events that need an immutable trail —
/// Phase 4 uses it for local-server URL change detection; future phases
/// (IdpOnly recovery, subscription lifecycle, ownership-proof challenges) will
/// append new <see cref="UserAuditAction"/> values without schema change.
/// </summary>
public class UserAuditLog : BaseTenantEntity
{
    /// <summary>
    /// User the event is about (and whose tenant the row is scoped to).
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Discriminator for the kind of event.
    /// </summary>
    public UserAuditAction Action { get; set; }

    /// <summary>
    /// JSON of the prior state (null on first-time events).
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON of the new state.
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Optional human-readable description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Caller IP (X-Forwarded-For first hop behind ALB; falls back to
    /// the direct RemoteIpAddress). Max 45 chars to fit IPv6.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Caller User-Agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    public virtual User User { get; set; } = null!;
}
