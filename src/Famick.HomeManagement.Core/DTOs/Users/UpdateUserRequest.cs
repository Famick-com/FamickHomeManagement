using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Request to update an existing user
/// </summary>
public class UpdateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<Role> Roles { get; set; } = new();
    public bool IsActive { get; set; }
}
