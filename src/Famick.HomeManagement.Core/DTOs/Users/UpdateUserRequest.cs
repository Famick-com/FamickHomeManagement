using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request to update an existing user. The sign-in email is set when the member joins and is not editable.
/// </summary>
public class UpdateUserRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<Role> Roles { get; set; } = new();
    public bool IsActive { get; set; }
}
