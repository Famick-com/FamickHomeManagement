using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Contacts;

public class CreateContactGroupRequest
{
    public ContactType ContactType { get; set; }

    public string GroupName { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public string? Website { get; set; }

    public string? BusinessCategory { get; set; }

    public List<Guid>? TagIds { get; set; }
}
