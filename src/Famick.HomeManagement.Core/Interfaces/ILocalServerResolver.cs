using Famick.HomeManagement.Domain.Entities;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Phase 4 chunk 4.D — resolves the canonical local-server URL for a user's
/// login response, persists the most-recent-delivered value on the user row,
/// and writes a <see cref="Famick.HomeManagement.Domain.Enums.UserAuditAction.LocalServerChanged"/>
/// audit-log entry when the value changes.
///
/// Single seam shared by every login surface (password, refresh-token, social,
/// passkey, accept-terms) so the change-detection contract is enforced once.
///
/// Behavior:
/// <list type="bullet">
/// <item>Cloud (multi-tenant) mode → returns <c>null</c> always. Cloud
///       accounts have no local server.</item>
/// <item>Self-hosted mode → reads <c>MobileAppSetup:PublicUrl</c> from
///       configuration. Empty/missing → returns <c>null</c>.</item>
/// <item>Otherwise canonicalizes the configured URL and compares against
///       <see cref="User.LastDeliveredLocalServer"/>. On first delivery
///       (stored value is null) → silent store. On mismatch → audit row
///       keyed on user_id. On no-op → no DB write.</item>
/// </list>
/// </summary>
public interface ILocalServerResolver
{
    Task<string?> ResolveAndAuditAsync(
        User user,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default);
}
