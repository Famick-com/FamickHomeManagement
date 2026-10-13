namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Thrown when an account is locked out after repeated failed sign-in attempts. Callers answer
/// 429 with <see cref="RetryAfter"/>.
/// </summary>
public class TooManyAttemptsException : AuthenticationException
{
    public TooManyAttemptsException(TimeSpan retryAfter)
        : base("Too many attempts. Please try again later.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}
