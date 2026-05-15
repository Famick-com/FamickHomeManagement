using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Writes rows to the user_audit_logs table. Used by Phase 4 for local-server
/// URL change detection on self-hosted login flows; designed to absorb future
/// user-scoped events (IdpOnly transitions, subscription changes) without
/// schema change.
///
/// IP and User-Agent are passed as strings — Core has no ASP.NET dependency,
/// so callers extract them from HttpContext at the controller layer.
/// </summary>
public interface IUserAuditLogger
{
    /// <summary>
    /// Persist one audit-log row. <paramref name="oldValues"/> and
    /// <paramref name="newValues"/> are serialized to jsonb via System.Text.Json
    /// (null becomes a SQL NULL, not the string "null").
    /// </summary>
    Task LogAsync(
        Guid userId,
        Guid tenantId,
        UserAuditAction action,
        object? oldValues,
        object? newValues,
        string? description,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
