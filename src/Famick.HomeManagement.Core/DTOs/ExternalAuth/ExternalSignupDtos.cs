namespace Famick.HomeManagement.Core.DTOs.ExternalAuth;

/// <summary>
/// A provider identity whose token signature and email have been checked. Handed to an
/// <see cref="Interfaces.IExternalSignupHandler"/> when no account matches it.
/// </summary>
public sealed record VerifiedExternalIdentity
{
    /// <summary>Canonical provider name ("Google", "Apple", "OIDC").</summary>
    public required string Provider { get; init; }

    /// <summary>The provider's stable subject identifier.</summary>
    public required string ProviderUserId { get; init; }

    /// <summary>The email the provider has verified, normalized.</summary>
    public required string Email { get; init; }

    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
}

/// <summary>
/// An offer to create an account for a <see cref="VerifiedExternalIdentity"/>. The ticket stands for
/// the verified identity until the user completes signup; it carries no access by itself.
/// </summary>
public sealed class ExternalSignupOffer
{
    /// <summary>Opaque, single-use reference to the verified identity.</summary>
    public string Ticket { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }

    /// <summary>
    /// Same-origin path where a browser completes signup, or null when only API clients can.
    /// </summary>
    public string? ContinueUrl { get; set; }
}
