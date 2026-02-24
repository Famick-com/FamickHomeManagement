using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Domain.Entities;

/// <summary>
/// Audit log entry for contact changes
/// </summary>
public class ContactAuditLog : BaseTenantEntity
{
    public Guid ContactId { get; set; }

    /// <summary>
    /// User who made the change
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of action performed
    /// </summary>
    public ContactAuditAction Action { get; set; }

    /// <summary>
    /// JSON representation of old values (for updates)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// JSON representation of new values (for creates/updates)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Human-readable description of the change
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// IP address of the user making the change (optional)
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string (optional)
    /// </summary>
    public string? UserAgent { get; set; }

    // Navigation properties
    public virtual Contact Contact { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
