namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Base exception for authentication-related errors
/// </summary>
public class AuthenticationException : Exception
{
    public AuthenticationException()
    {
    }

    public AuthenticationException(string message) : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
