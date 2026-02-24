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
