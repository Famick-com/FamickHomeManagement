namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>
/// Exception thrown when attempting to authenticate with an inactive user account
/// </summary>
public class AccountInactiveException : AuthenticationException
{
    public AccountInactiveException() : base("Account is inactive")
    {
    }

    public AccountInactiveException(string message) : base(message)
    {
    }

    public AccountInactiveException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
