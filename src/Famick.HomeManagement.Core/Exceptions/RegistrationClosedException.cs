namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when self-service account creation is attempted on a server that has already
/// been set up.
/// </summary>
/// <remarks>
/// Distinct from <see cref="DuplicateEntityException"/> and the generic
/// <see cref="InvalidOperationException"/> so callers can answer <c>403</c> rather than <c>400</c> —
/// the request is well-formed, the server simply does not offer registration any more. That matches
/// the status <c>AuthApiController.Register</c> already returns for the same condition on the
/// password path.
/// </remarks>
public class RegistrationClosedException : AuthenticationException
{
    public RegistrationClosedException()
        : base("Registration is closed. The system has already been set up.")
    {
    }

    public RegistrationClosedException(string message) : base(message)
    {
    }

    public RegistrationClosedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
