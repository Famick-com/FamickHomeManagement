using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

public class ContactGroupSummaryDto
{
    public Guid Id { get; set; }

    public ContactType ContactType { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string? ProfileImageUrl { get; set; }

    public int MemberCount { get; set; }

    public string? PrimaryAddress { get; set; }

    public bool IsTenantHousehold { get; set; }

    public List<string> TagNames { get; set; } = new();

    public List<string?> TagColors { get; set; } = new();

    public string? Website { get; set; }

    public string? BusinessCategory { get; set; }

    public DateTime CreatedAt { get; set; }
}
