using Amazon;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// AWS SES v2-based email service implementation
/// </summary>
public class AwsSesEmailService : IEmailService, IDisposable
{
    private readonly EmailSettings _settings;
    private readonly ILogger<AwsSesEmailService> _logger;
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private bool _disposed;

    public AwsSesEmailService(
        IOptions<EmailSettings> settings,
        ILogger<AwsSesEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _sesClient = CreateSesClient();
    }

    private IAmazonSimpleEmailServiceV2 CreateSesClient()
    {
        var region = RegionEndpoint.GetBySystemName(_settings.AwsSes.Region);

        if (_settings.AwsSes.UseInstanceCredentials)
        {
            // Use IAM role credentials (EC2, ECS, App Runner, Lambda)
            return new AmazonSimpleEmailServiceV2Client(region);
        }

        // Use explicit credentials
        if (string.IsNullOrEmpty(_settings.AwsSes.AccessKeyId) ||
            string.IsNullOrEmpty(_settings.AwsSes.SecretAccessKey))
        {
            throw new InvalidOperationException(
                "AWS SES credentials not configured. Set UseInstanceCredentials=true or provide AccessKeyId and SecretAccessKey.");
        }

        return new AmazonSimpleEmailServiceV2Client(
            _settings.AwsSes.AccessKeyId,
            _settings.AwsSes.SecretAccessKey,
            region);
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

    /// <inheritdoc />
    public async Task SendRawEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        await SendEmailAsync(toEmail, subject, htmlBody, textBody, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendNotificationEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string unsubscribeUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.FromEmail))
        {
            _logger.LogWarning("From email not configured. Notification email to {Email} not sent.", toEmail);
            return;
        }

        try
        {
            var source = string.IsNullOrEmpty(_settings.FromName)
                ? _settings.FromEmail
                : $"{_settings.FromName} <{_settings.FromEmail}>";

            // A raw message is what carries the List-Unsubscribe headers; the simple send
            // used elsewhere cannot set them.
            var rawMessage = BuildRawNotificationMessage(
                source, toEmail, subject, htmlBody, textBody, unsubscribeUrl);

            var request = new SendEmailRequest
            {
                Content = new EmailContent
                {
                    Raw = new RawMessage
                    {
                        Data = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawMessage))
                    }
                }
            };

            if (!string.IsNullOrEmpty(_settings.AwsSes.ConfigurationSetName))
            {
                request.ConfigurationSetName = _settings.AwsSes.ConfigurationSetName;
            }

            var response = await _sesClient.SendEmailAsync(request, cancellationToken);
            _logger.LogInformation("Notification email sent via SES to {Email}. MessageId: {MessageId}", toEmail, response.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification email via SES to {Email}", toEmail);
            throw;
        }
    }

    /// <summary>
    /// Assembles the RFC 5322 message sent for a notification email.
    /// </summary>
    /// <remarks>
    /// The header block is built as a list of lines and joined, rather than interpolated into
    /// a template. An earlier version interpolated an optional two-line unsubscribe block
    /// straight before <c>Content-Type</c>; the block carried no trailing newline, so whenever
    /// an unsubscribe URL was present the two ran together as
    /// <c>List-Unsubscribe=One-ClickContent-Type: multipart/alternative</c>. That left the
    /// message with no Content-Type at all, and a message with no Content-Type is plain text —
    /// so every recipient saw the MIME boundaries, the part headers and the raw HTML instead
    /// of the message. Joining a list cannot lose a separator.
    /// </remarks>
    public static string BuildRawNotificationMessage(
        string source,
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        string unsubscribeUrl)
    {
        const string boundary = "boundary123";

        var headers = new List<string>
        {
            $"From: {source}",
            $"To: {toEmail}",
            $"Subject: {EncodeHeaderValue(subject)}",
            "MIME-Version: 1.0"
        };

        // RFC 2369 / RFC 8058. Only present when there is somewhere to unsubscribe to.
        if (!string.IsNullOrEmpty(unsubscribeUrl))
        {
            headers.Add($"List-Unsubscribe: <{unsubscribeUrl}>");
            headers.Add("List-Unsubscribe-Post: List-Unsubscribe=One-Click");
        }

        headers.Add($"Content-Type: multipart/alternative; boundary=\"{boundary}\"");

        // Both parts carry UTF-8 as-is. Without an explicit transfer encoding the default is
        // 7bit, which product names alone are enough to violate — "Kroger® Ground Cumin".
        return string.Join("\n", headers)
            + "\n\n"
            + $"--{boundary}\n"
            + "Content-Type: text/plain; charset=UTF-8\n"
            + "Content-Transfer-Encoding: 8bit\n\n"
            + textBody + "\n\n"
            + $"--{boundary}\n"
            + "Content-Type: text/html; charset=UTF-8\n"
            + "Content-Transfer-Encoding: 8bit\n\n"
            + htmlBody + "\n\n"
            + $"--{boundary}--";
    }

    /// <summary>
    /// Encodes a header value as an RFC 2047 encoded-word when it is not plain ASCII.
    /// </summary>
    /// <remarks>
    /// Header values are ASCII by definition. A calendar reminder takes its subject from an
    /// event title someone typed, so an accent or a curly quote is a matter of time, and
    /// putting those bytes in a header raw leaves the subject to chance.
    /// </remarks>
    private static string EncodeHeaderValue(string value)
    {
        var needsEncoding = false;
        foreach (var c in value)
        {
            if (c > 127) { needsEncoding = true; break; }
        }

        if (!needsEncoding) return value;

        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        return $"=?UTF-8?B?{Convert.ToBase64String(bytes)}?=";
    }

    private async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_settings.FromEmail))
        {
            _logger.LogWarning("From email not configured. Email to {Email} not sent.", toEmail);
            return;
        }

        try
        {
            var fromAddress = string.IsNullOrEmpty(_settings.FromName)
                ? _settings.FromEmail
                : $"{_settings.FromName} <{_settings.FromEmail}>";

            var request = new SendEmailRequest
            {
                FromEmailAddress = fromAddress,
                Destination = new Destination
                {
                    ToAddresses = new List<string> { toEmail }
                },
                Content = new EmailContent
                {
                    Simple = new Message
                    {
                        Subject = new Content { Data = subject },
                        Body = new Body
                        {
                            Html = new Content
                            {
                                Charset = "UTF-8",
                                Data = htmlBody
                            },
                            Text = new Content
                            {
                                Charset = "UTF-8",
                                Data = textBody
                            }
                        }
                    }
                }
            };

            // Add reply-to addresses if configured
            if (_settings.ReplyToAddresses.Count > 0)
            {
                request.ReplyToAddresses = _settings.ReplyToAddresses;
            }

            // Add configuration set if specified
            if (!string.IsNullOrEmpty(_settings.AwsSes.ConfigurationSetName))
            {
                request.ConfigurationSetName = _settings.AwsSes.ConfigurationSetName;
            }

            var response = await _sesClient.SendEmailAsync(request, cancellationToken);

            _logger.LogInformation(
                "Email sent successfully via SES to {Email}. MessageId: {MessageId}",
                toEmail,
                response.MessageId);
        }
        catch (AccountSuspendedException ex)
        {
            _logger.LogError(ex, "SES account suspended. Email to {Email} not sent.", toEmail);
            throw;
        }
        catch (MailFromDomainNotVerifiedException ex)
        {
            _logger.LogError(ex, "SES mail from domain not verified for {Email}", toEmail);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via SES to {Email}", toEmail);
            throw;
        }
    }

    #region Email Templates (shared with SmtpEmailService - consider extracting to shared class)

    private static string GenerateEmailVerificationHtml(string householdName, string verificationLink)
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
                    <h2>Verify Your Email</h2>
                    <p>Welcome to Famick Home Management!</p>
                    <p>You're creating a new household called <strong>{{householdName}}</strong>.</p>
                    <p>Click the button below to verify your email and complete your registration:</p>
                    <a href="{{verificationLink}}" class="button">Verify Email</a>
                    <p>This link will expire in 24 hours.</p>
                    <p>If you didn't request this, you can safely ignore this email.</p>
                    <div class="footer">
                        <p>If the button doesn't work, copy and paste this link into your browser:</p>
                        <p>{{verificationLink}}</p>
                    </div>
                </div>
            </body>
            </html>
            """;
    }

    private static string GenerateEmailVerificationText(string householdName, string verificationLink)
    {
        return $"""
            Verify Your Email

            Welcome to Famick Home Management!

            You're creating a new household called {householdName}.

            Click the link below to verify your email and complete your registration:
            {verificationLink}

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

    #endregion

    public void Dispose()
    {
        if (!_disposed)
        {
            _sesClient.Dispose();
            _disposed = true;
        }
    }
}
