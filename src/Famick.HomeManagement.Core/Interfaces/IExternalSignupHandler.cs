using Famick.HomeManagement.Core.DTOs.ExternalAuth;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Optional seam for hosts that let a new account start from an external provider sign-in.
/// </summary>
/// <remarks>
/// <see cref="IExternalAuthService"/> consults it only when the provider has verified the email and
/// no account matches the identity or the email. Accounts that already exist never reach it, so it
/// cannot link or merge anything. With no handler registered — the self-hosted default — an unknown
/// email is refused as before.
/// </remarks>
public interface IExternalSignupHandler
{
    /// <summary>
    /// Returns an offer to sign up with <paramref name="identity"/>, or null to refuse.
    /// </summary>
    Task<ExternalSignupOffer?> OfferAsync(
        VerifiedExternalIdentity identity,
        string ipAddress,
        CancellationToken cancellationToken = default);
}
