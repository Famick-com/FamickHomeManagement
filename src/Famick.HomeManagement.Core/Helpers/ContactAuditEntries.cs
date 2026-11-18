using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.Helpers;

/// <summary>
/// Audit entries for changes nobody signed in to this household made.
/// </summary>
public static class ContactAuditEntries
{
    /// <summary>
    /// Records that <paramref name="householdName"/> changed the details it keeps on a copy held
    /// in <paramref name="tenantId"/>. The entry has no user, so it reads as the household's.
    /// </summary>
    public static ContactAuditLog FromLinkedHousehold(
        Guid tenantId, Guid contactId, string householdName, string? oldValues = null, string? newValues = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        ContactId = contactId,
        UserId = null,
        Action = ContactAuditAction.UpdatedByLinkedHousehold,
        OldValues = oldValues,
        NewValues = newValues,
        Description = $"Updated by {householdName}"
    };
}
