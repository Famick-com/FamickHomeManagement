using System.ComponentModel.DataAnnotations;

namespace Famick.HomeManagement.Core.DTOs.Transfer;

/// <summary>
/// Request to authenticate against the cloud instance for data transfer.
/// </summary>
public class TransferAuthenticateRequest
{
    /// <summary>
    /// Cloud account email address
    /// </summary>
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Cloud account password
    /// </summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Whether to register a new cloud account instead of logging in
    /// </summary>
    public bool IsRegistration { get; set; }

    /// <summary>
    /// First name (required if IsRegistration is true)
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Last name (required if IsRegistration is true)
    /// </summary>
    public string? LastName { get; set; }
}

/// <summary>
/// Response from cloud authentication
/// </summary>
public class TransferAuthenticateResponse
{
    public bool Success { get; set; }
    public string? CloudUserEmail { get; set; }
    public string? ErrorMessage { get; set; }
}
