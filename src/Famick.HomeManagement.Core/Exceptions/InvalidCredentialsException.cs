namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when user credentials are invalid
/// </summary>
public class InvalidCredentialsException : AuthenticationException
{
    public InvalidCredentialsException() : base("Invalid email or password")
    {
    }

    public InvalidCredentialsException(string message) : base(message)
    {
    }

    public InvalidCredentialsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
