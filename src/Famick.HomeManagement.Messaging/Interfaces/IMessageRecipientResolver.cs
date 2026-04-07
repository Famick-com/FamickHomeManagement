namespace Famick.HomeManagement.Messaging.Interfaces;

/// <summary>
/// Resolves recipient information for the messaging service.
/// Implemented by the infrastructure layer to query user data.
/// </summary>
public interface IMessageRecipientResolver
{
    /// <summary>
    /// Resolves recipient details for a user by ID.
    /// Returns null if the user is not found or inactive.
    /// </summary>
    Task<MessageRecipient?> ResolveAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Recipient information needed by the messaging service.
/// </summary>
public record MessageRecipient(
    Guid UserId,
    string Email,
    string? PhoneNumber,
    string FirstName,
    string LastName,
    Guid TenantId,
    bool IsActive)
{
    public string FullName => $"{FirstName} {LastName}".Trim();
}
