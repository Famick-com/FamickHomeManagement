namespace Famick.HomeManagement.Mobile.Services;

/// <summary>
/// Service for native Google Sign-In functionality.
/// Platform-specific implementations use the native Google Sign-In SDK.
/// </summary>
public interface IGoogleSignInService
{
    /// <summary>
    /// Whether native Google Sign-In is available on this platform.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Performs native Google Sign-In and returns the credentials.
    /// </summary>
    /// <returns>Google credentials containing ID token and user info</returns>
    /// <exception cref="OperationCanceledException">User cancelled the sign-in</exception>
    /// <exception cref="GoogleSignInException">Sign-in failed</exception>
    Task<GoogleSignInCredential> SignInAsync();

    /// <summary>
    /// Signs out the current user from Google.
    /// </summary>
    Task SignOutAsync();
}

/// <summary>
/// Credentials returned from native Google Sign-In.
/// </summary>
public class GoogleSignInCredential
{
    /// <summary>
    /// The user's Google ID (sub claim).
    /// </summary>
    public string UserId { get; set; } = "";

    /// <summary>
    /// The ID token (JWT) from Google.
    /// </summary>
    public string IdToken { get; set; } = "";

    /// <summary>
    /// User's email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// User's display name.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// User's given (first) name.
    /// </summary>
    public string? GivenName { get; set; }

    /// <summary>
    /// User's family (last) name.
    /// </summary>
    public string? FamilyName { get; set; }

    /// <summary>
    /// URL to user's profile photo.
    /// </summary>
    public string? PhotoUrl { get; set; }
}

/// <summary>
/// Exception thrown when Google Sign-In fails.
/// </summary>
public class GoogleSignInException : Exception
{
    public GoogleSignInErrorCode ErrorCode { get; }

    public GoogleSignInException(string message, GoogleSignInErrorCode errorCode)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public GoogleSignInException(string message, GoogleSignInErrorCode errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Error codes for Google Sign-In failures.
/// </summary>
public enum GoogleSignInErrorCode
{
    Unknown = 0,
    Canceled = 1,
    NetworkError = 2,
    InvalidAccount = 3,
    InternalError = 4,
    NotAvailable = 5,
    ConfigurationError = 6
}
