using Famick.HomeManagement.Core.DTOs.ExternalAuth;

namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Thrown by an external sign-in when no account exists for the identity but the host offers to
/// create one. Callers that know about signup answer with the <see cref="Offer"/>.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidCredentialsException"/> so any caller that does not handle it
/// still refuses the sign-in with the same message as before.
/// </remarks>
public class ExternalSignupRequiredException : InvalidCredentialsException
{
    public const string DefaultMessage = "No account exists with this email address.";

    public ExternalSignupRequiredException(ExternalSignupOffer offer)
        : base(DefaultMessage)
    {
        Offer = offer;
    }

    public ExternalSignupOffer Offer { get; }
}
