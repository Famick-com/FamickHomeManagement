using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

/// <summary>
/// Contact audit log entry
/// </summary>
public class ContactAuditLogDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// Name of the user who made the change
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// Action performed
    /// </summary>
    public ContactAuditAction Action { get; set; }

    /// <summary>
    /// Previous values (JSON)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (JSON)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// Human-readable description of the change
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent of the request
    /// </summary>
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}
