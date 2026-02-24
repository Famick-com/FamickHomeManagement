namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for native Apple Sign in functionality.
/// Platform-specific implementations use the native Apple Sign in SDK.
/// </summary>
public interface IAppleSignInService
{
    /// <summary>
    /// Whether native Apple Sign in is available on this platform.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Performs native Apple Sign in and returns the credentials.
    /// </summary>
    /// <returns>Apple credentials containing identity token and user info</returns>
    /// <exception cref="OperationCanceledException">User cancelled the sign-in</exception>
    /// <exception cref="AppleSignInException">Sign-in failed</exception>
    Task<AppleSignInCredential> SignInAsync();
}

/// <summary>
/// Credentials returned from native Apple Sign in.
/// </summary>
public class AppleSignInCredential
{
    /// <summary>
    /// The user's stable identifier (same across all apps from the same developer).
    /// </summary>
    public string UserIdentifier { get; set; } = "";

    /// <summary>
    /// The identity token (JWT) from Apple.
    /// </summary>
    public string IdentityToken { get; set; } = "";

    /// <summary>
    /// The authorization code (can be used for server-side validation).
    /// </summary>
    public string? AuthorizationCode { get; set; }

    /// <summary>
    /// User's given (first) name. Only provided on first sign-in.
    /// </summary>
    public string? GivenName { get; set; }

    /// <summary>
    /// User's family (last) name. Only provided on first sign-in.
    /// </summary>
    public string? FamilyName { get; set; }

    /// <summary>
    /// User's email. Only provided on first sign-in, may be a relay email.
    /// </summary>
    public string? Email { get; set; }
}

/// <summary>
/// Exception thrown when Apple Sign in fails.
/// </summary>
public class AppleSignInException : Exception
{
    public AppleSignInErrorCode ErrorCode { get; }

    public AppleSignInException(string message, AppleSignInErrorCode errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public AppleSignInException(string message, AppleSignInErrorCode errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Error codes for Apple Sign in failures.
/// </summary>
public enum AppleSignInErrorCode
{
    Unknown = 0,
    Canceled = 1,
    InvalidResponse = 2,
    NotHandled = 3,
    Failed = 4,
    NotAvailable = 5
}
