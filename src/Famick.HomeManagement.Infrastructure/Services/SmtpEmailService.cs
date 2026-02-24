using System.Net;
using System.Net.Mail;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// SMTP-based email service implementation
/// </summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(
        IOptions<EmailSettings> settings,
        ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendEmailVerificationAsync(
        string toEmail,
        string householdName,
        string verificationLink,
        CancellationToken cancellationToken = default)
    {
        var subject = "Verify Your Email - Famick Home Management";
        var htmlBody = GenerateEmailVerificationHtml(householdName, verificationLink);
        var textBody = GenerateEmailVerificationText(householdName, verificationLink);

        await SendEmailAsync(toEmail, subject, htmlBody, textBody, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendPasswordResetEmailAsync(
        string toEmail,
        string userName,
        string resetLink,
        CancellationToken cancellationToken = default)
    {
        var subject = "Reset Your Password - Famick Home Management";
        var htmlBody = GeneratePasswordResetEmailHtml(userName, resetLink);
        var textBody = GeneratePasswordResetEmailText(userName, resetLink);

        await SendEmailAsync(toEmail, subject, htmlBody, textBody, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendPasswordResetConfirmationEmailAsync(
        string toEmail,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var subject = "Password Changed - Famick Home Management";
        var htmlBody = GeneratePasswordChangedEmailHtml(userName);
        var textBody = GeneratePasswordChangedEmailText(userName);

        await SendEmailAsync(toEmail, subject, htmlBody, textBody, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendNotificationEmailAsync(
        string toEmail,
        string userName,
        string subject,
        string htmlBody,
        string textBody,
        string unsubscribeToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.Smtp.Host))
        {
            _logger.LogWarning("SMTP host not configured. Notification email to {Email} not sent.", toEmail);
            return;
        }

        try
        {
            using var client = new SmtpClient(_settings.Smtp.Host, _settings.Smtp.Port)
            {
                EnableSsl = _settings.Smtp.EnableSsl,
                Credentials = !string.IsNullOrEmpty(_settings.Smtp.Username)
                    ? new NetworkCredential(_settings.Smtp.Username, _settings.Smtp.Password)
                    : null
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                IsBodyHtml = true,
                Body = htmlBody
            };

            message.To.Add(toEmail);

            // Add RFC 8058 List-Unsubscribe headers
            var unsubscribeUrl = $"{_settings.FromEmail.Replace("notifications@", "https://")}/api/v1/notifications/unsubscribe?token={unsubscribeToken}";
            message.Headers.Add("List-Unsubscribe", $"<{unsubscribeUrl}>");
            message.Headers.Add("List-Unsubscribe-Post", "List-Unsubscribe=One-Click");

            // Add plain text alternative
            var plainTextView = AlternateView.CreateAlternateViewFromString(
                textBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody, null, "text/html");

            message.AlternateViews.Add(plainTextView);
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("Notification email sent to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email to {Email}", toEmail);
            throw;
        }
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.Smtp.Host))
        {
            _logger.LogWarning("SMTP host not configured. Email to {Email} not sent.", toEmail);
            return;
        }

        try
        {
            using var client = new SmtpClient(_settings.Smtp.Host, _settings.Smtp.Port)
            {
                EnableSsl = _settings.Smtp.EnableSsl,
                Credentials = !string.IsNullOrEmpty(_settings.Smtp.Username)
                    ? new NetworkCredential(_settings.Smtp.Username, _settings.Smtp.Password)
                    : null
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromEmail, _settings.FromName),
                Subject = subject,
                IsBodyHtml = true,
                Body = htmlBody
            };

            message.To.Add(toEmail);

            // Add reply-to addresses if configured
            foreach (var replyTo in _settings.ReplyToAddresses)
            {
                message.ReplyToList.Add(replyTo);
            }

            // Add plain text alternative
            var plainTextView = AlternateView.CreateAlternateViewFromString(
                textBody, null, "text/plain");
            var htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody, null, "text/html");

            message.AlternateViews.Add(plainTextView);
            message.AlternateViews.Add(htmlView);

            await client.SendMailAsync(message, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Email}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email}", toEmail);
            throw;
        }
    }

    private static string GenerateEmailVerificationHtml(string householdName, string verificationLink)
    {
        // Extract token from the link for manual entry fallback
        var token = verificationLink.Contains("token=")
            ? verificationLink.Split("token=")[1]
            : verificationLink;

        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                    .button { display: inline-block; padding: 12px 24px; background-color: #1976D2;
                               color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }
                    .token-box { background-color: #f5f5f5; padding: 15px; border-radius: 4px;
                                  font-family: monospace; word-break: break-all; margin: 15px 0; }
                    .footer { margin-top: 30px; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <div class="container">
                    <h2>Verify Your Email</h2>
                    <p>Welcome to Famick Home Management!</p>
                    <p>You're creating a new household called <strong>{{householdName}}</strong>.</p>
                    <p>Click the button below to verify your email and complete your registration:</p>
                    <a href="{{verificationLink}}" class="button">Verify Email</a>
                    <p>This link will expire in 24 hours.</p>
                    <p><strong>If the button doesn't open the app</strong>, copy this verification token and paste it in the app:</p>
                    <div class="token-box">{{token}}</div>
                    <p>If you didn't request this, you can safely ignore this email.</p>
                </div>
            </body>
            </html>
            """;
    }

    private static string GenerateEmailVerificationText(string householdName, string verificationLink)
    {
        // Extract token from the link for manual entry fallback
        var token = verificationLink.Contains("token=")
            ? verificationLink.Split("token=")[1]
            : verificationLink;

        return $"""
            Verify Your Email

            Welcome to Famick Home Management!

            You're creating a new household called {householdName}.

            Click the link below to verify your email and complete your registration:
            {verificationLink}

            If the link doesn't open the app, copy this verification token and paste it in the app:
            {token}

            This link will expire in 24 hours.

            If you didn't request this, you can safely ignore this email.
            """;
    }

    private static string GeneratePasswordResetEmailHtml(string userName, string resetLink)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                    .button { display: inline-block; padding: 12px 24px; background-color: #1976D2;
                               color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }
                    .footer { margin-top: 30px; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <div class="container">
                    <h2>Reset Your Password</h2>
                    <p>Hi {{userName}},</p>
                    <p>We received a request to reset your password for your Famick Home Management account.</p>
                    <p>Click the button below to reset your password:</p>
                    <a href="{{resetLink}}" class="button">Reset Password</a>
                    <p>This link will expire in 15 minutes.</p>
                    <p>If you didn't request a password reset, you can safely ignore this email.
                       Your password will remain unchanged.</p>
                    <div class="footer">
                        <p>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p>{{resetLink}}</p>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private static string GeneratePasswordResetEmailText(string userName, string resetLink)
    {
        return $"""
            Reset Your Password

            Hi {userName},

            We received a request to reset your password for your Famick Home Management account.

            Click the link below to reset your password:
            {resetLink}

            This link will expire in 15 minutes.

            If you didn't request a password reset, you can safely ignore this email.
            Your password will remain unchanged.
            """;
    }

    private static string GeneratePasswordChangedEmailHtml(string userName)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                </style>
            </head>
            <body>
                <div class="container">
                    <h2>Password Changed Successfully</h2>
                    <p>Hi {{userName}},</p>
                    <p>Your password for Famick Home Management has been successfully changed.</p>
                    <p>If you did not make this change, please contact support immediately.</p>
                </div>
            </body>
            </html>
            """;
    }

    private static string GeneratePasswordChangedEmailText(string userName)
    {
        return $"""
            Password Changed Successfully

            Hi {userName},

            Your password for Famick Home Management has been successfully changed.

            If you did not make this change, please contact support immediately.
            """;
    }

    /// <inheritdoc />
    public async Task SendWelcomeEmailAsync(
        string toEmail,
        string userName,
        string temporaryPassword,
        string loginUrl,
        CancellationToken cancellationToken = default)
    {
        var subject = "Welcome to Famick Home Management";
        var htmlBody = GenerateWelcomeEmailHtml(userName, toEmail, temporaryPassword, loginUrl);
        var textBody = GenerateWelcomeEmailText(userName, toEmail, temporaryPassword, loginUrl);

        await SendEmailAsync(toEmail, subject, htmlBody, textBody, cancellationToken);
    }

    private static string GenerateWelcomeEmailHtml(string userName, string email, string temporaryPassword, string loginUrl)
    {
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }
                    .container { max-width: 600px; margin: 0 auto; padding: 20px; }
                    .button { display: inline-block; padding: 12px 24px; background-color: #1976D2;
                               color: white; text-decoration: none; border-radius: 4px; margin: 20px 0; }
                    .credentials { background-color: #f5f5f5; padding: 15px; border-radius: 4px; margin: 20px 0; }
                    .credentials p { margin: 5px 0; }
                    .warning { color: #d32f2f; font-weight: bold; }
                    .footer { margin-top: 30px; font-size: 12px; color: #666; }
                </style>
            </head>
            <body>
                <div class="container">
                    <h2>Welcome to Famick Home Management</h2>
                    <p>Hi {{userName}},</p>
                    <p>An account has been created for you. Here are your login credentials:</p>
                    <div class="credentials">
                        <p><strong>Email:</strong> {{email}}</p>
                        <p><strong>Temporary Password:</strong> {{temporaryPassword}}</p>
                    </div>
                    <a href="{{loginUrl}}" class="button">Login to Famick</a>
                    <p class="warning">Please change your password after your first login.</p>
                    <p>If you did not expect this email, please contact your administrator.</p>
                    <div class="footer">
                        <p>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p>{{loginUrl}}</p>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private static string GenerateWelcomeEmailText(string userName, string email, string temporaryPassword, string loginUrl)
    {
        return $"""
            Welcome to Famick Home Management

            Hi {userName},

            An account has been created for you. Here are your login credentials:

            Email: {email}
            Temporary Password: {temporaryPassword}

            Login here: {loginUrl}

            Please change your password after your first login.

            If you did not expect this email, please contact your administrator.
            """;
    }
}
