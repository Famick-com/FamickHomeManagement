namespace Famick.HomeManagement.Core.Configuration;

/// <summary>
/// Email provider type
/// </summary>
public enum EmailProvider
{
    /// <summary>
    /// Standard SMTP server
    /// </summary>
    Smtp,

    /// <summary>
    /// Amazon Simple Email Service
    /// </summary>
    AwsSes
}

/// <summary>
/// Email configuration settings supporting multiple providers
/// </summary>
public class EmailSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "EmailSettings";

    /// <summary>
    /// Email provider to use (Smtp or AwsSes)
    /// </summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Smtp;

    /// <summary>
    /// From email address
    /// </summary>
    public string FromEmail { get; set; } = string.Empty;

    /// <summary>
    /// From display name
    /// </summary>
    public string FromName { get; set; } = "Famick Home Management";

    /// <summary>
    /// Reply-to email addresses (optional)
    /// </summary>
    public List<string> ReplyToAddresses { get; set; } = new();

    /// <summary>
    /// Password reset token expiration in minutes
    /// </summary>
    public int PasswordResetTokenExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Lifetime, in hours, of the link emailed when an admin resets a member's password.
    /// </summary>
    /// <remarks>
    /// Longer than <see cref="PasswordResetTokenExpirationMinutes"/> because the member did not ask
    /// for it and may not see the email for a while, and until they use it they cannot sign in.
    /// </remarks>
    public int AdminPasswordResetTokenExpirationHours { get; set; } = 24;

    /// <summary>
    /// Whether the server can send email at all: a sender address, plus an SMTP host when the
    /// provider is SMTP. A self-hosted install with neither cannot, and falls back to flows that
    /// do not need mail (an admin handing a member a temporary password).
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(FromEmail)
        && (Provider == EmailProvider.AwsSes || !string.IsNullOrWhiteSpace(Smtp.Host));

    /// <summary>
    /// SMTP-specific settings
    /// </summary>
    public SmtpSettings Smtp { get; set; } = new();

    /// <summary>
    /// AWS SES-specific settings
    /// </summary>
    public AwsSesSettings AwsSes { get; set; } = new();
}

/// <summary>
/// SMTP server configuration
/// </summary>
public class SmtpSettings
{
    /// <summary>
    /// SMTP server host
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// SMTP server port (typically 587 for TLS, 465 for SSL)
    /// </summary>
    public int Port { get; set; } = 587;

    /// <summary>
    /// SMTP username for authentication
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// SMTP password for authentication
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Enable SSL/TLS for SMTP connection
    /// </summary>
    public bool EnableSsl { get; set; } = true;
}

/// <summary>
/// AWS SES configuration
/// </summary>
public class AwsSesSettings
{
    /// <summary>
    /// AWS region for SES (e.g., us-east-1)
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// SES Configuration Set name (optional, for tracking)
    /// </summary>
    public string? ConfigurationSetName { get; set; }

    /// <summary>
    /// Use IAM role credentials (true for EC2/ECS/App Runner, false to use access keys)
    /// </summary>
    public bool UseInstanceCredentials { get; set; } = true;

    /// <summary>
    /// AWS Access Key ID (only used if UseInstanceCredentials is false)
    /// </summary>
    public string? AccessKeyId { get; set; }

    /// <summary>
    /// AWS Secret Access Key (only used if UseInstanceCredentials is false)
    /// </summary>
    public string? SecretAccessKey { get; set; }
}
