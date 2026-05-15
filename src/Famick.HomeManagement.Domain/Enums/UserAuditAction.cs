namespace Famick.HomeManagement.Domain.Enums;

/// <summary>
/// Actions tracked in user audit logs. Stored as strings in the DB
/// (see UserAuditLogConfiguration), so renaming a value is a breaking change —
/// add new values, don't reuse old names. Numeric values are advisory only.
/// </summary>
public enum UserAuditAction
{
    // Phase 4 — local-server URL change detection on self-hosted LoginResponse.
    LocalServerChanged = 1,
}
