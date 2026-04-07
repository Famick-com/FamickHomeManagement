namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Service for sending emails
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email verification link for new user registration.
    /// </summary>
    [Obsolete("Use IMessageService.SendTransactionalAsync with MessageType.EmailVerification instead")]
    /// <param name="toEmail">The recipient's email address</param>
    /// <param name="householdName">The household name being registered</param>
    /// <param name="verificationLink">The email verification link (deep link for mobile)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendEmailVerificationAsync(
        string toEmail,
        string householdName,
        string verificationLink,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a password reset email to the user.
    /// </summary>
    [Obsolete("Use IMessageService.SendTransactionalAsync with MessageType.PasswordReset instead")]
    /// <param name="toEmail">The recipient's email address</param>
    /// <param name="userName">The user's display name</param>
    /// <param name="resetLink">The password reset link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendPasswordResetEmailAsync(
        string toEmail,
        string userName,
        string resetLink,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a password reset confirmation email after successful reset.
    /// </summary>
    [Obsolete("Use IMessageService.SendTransactionalAsync with MessageType.PasswordChanged instead")]
    /// <param name="toEmail">The recipient's email address</param>
    /// <param name="userName">The user's display name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendPasswordResetConfirmationEmailAsync(
        string toEmail,
        string userName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a welcome email to a newly created user with their login credentials.
    /// </summary>
    [Obsolete("Use IMessageService.SendTransactionalAsync with MessageType.Welcome instead")]
    /// <param name="toEmail">The recipient's email address</param>
    /// <param name="userName">The user's display name</param>
    /// <param name="temporaryPassword">The temporary password for initial login</param>
    /// <param name="loginUrl">The URL where the user can log in</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendWelcomeEmailAsync(
        string toEmail,
        string userName,
        string temporaryPassword,
        string loginUrl,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a raw email with pre-rendered content (no template logic).
    /// Used by the unified messaging service for transactional emails.
    /// </summary>
    Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a notification email with RFC 2369 List-Unsubscribe and RFC 8058 One-Click headers.
    /// </summary>
    /// <param name="toEmail">The recipient's email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="htmlBody">HTML body content (compliance footer already included)</param>
    /// <param name="textBody">Plain text body content</param>
    /// <param name="unsubscribeUrl">Full unsubscribe URL for List-Unsubscribe header</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SendNotificationEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string unsubscribeUrl,
        CancellationToken cancellationToken = default);
}
