namespace Famick.HomeManagement.Core.Exceptions;

/// <summary>A shared item has more in it than a copy may carry: too many steps, lines, recipes or items.</summary>
public class ShareTooLargeException : DomainException
{
    public ShareTooLargeException(string message) : base(message) { }
}
