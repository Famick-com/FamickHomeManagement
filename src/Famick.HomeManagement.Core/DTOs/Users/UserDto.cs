using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.Users;

/// <summary>
/// Data transfer object for user management
/// </summary>
public class ManagedUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<Role> Roles { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    // Contact linking
    public Guid? ContactId { get; set; }
    public string? ContactName { get; set; }
}
