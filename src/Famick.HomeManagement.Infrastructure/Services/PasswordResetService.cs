using Famick.HomeManagement.Core.Helpers;
using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Messaging.DTOs;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Common;
using Famick.HomeManagement.Messaging.Interfaces;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for handling password reset operations
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    private readonly HomeManagementDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IMessageService _messageService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<PasswordResetService> _logger;

    // Rate limiting: max 3 requests per email per hour
    private const int MaxRequestsPerHour = 3;

    public PasswordResetService(
        HomeManagementDbContext context,
        IEmailService emailService,
        IMessageService messageService,
        IPasswordHasher passwordHasher,
        IOptions<EmailSettings> emailSettings,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _emailService = emailService;
        _messageService = messageService;
        _passwordHasher = passwordHasher;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ForgotPasswordResponse> RequestPasswordResetAsync(
        ForgotPasswordRequest request,
        string ipAddress,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var email = EmailAddress.Normalize(request.Email);

        // Always return success message (prevent email enumeration)
        var response = new ForgotPasswordResponse();

        // Find user by email
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email && u.IsActive, cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Password reset requested for non-existent email: {Email}", email);
            return response; // Return success to prevent enumeration
        }

        // Check rate limiting - max requests per hour for this email
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        var recentRequests = await _context.PasswordResetTokens
            .CountAsync(t => t.UserId == user.Id && t.CreatedAt > oneHourAgo, cancellationToken);

        if (recentRequests >= MaxRequestsPerHour)
        {
            _logger.LogWarning("Rate limit exceeded for password reset. Email: {Email}, IP: {IP}", email, ipAddress);
            return response; // Still return success to prevent enumeration
        }

        var token = await IssueTokenAsync(
            user, TimeSpan.FromMinutes(_emailSettings.PasswordResetTokenExpirationMinutes), ipAddress, cancellationToken);
        var resetLink = ResetLink(baseUrl, token);

        // Send email (fire and forget with error handling)
        try
        {
            await _messageService.SendTransactionalAsync(
                user.Email,
                MessageType.PasswordReset,
                new PasswordResetData
                {
                    UserName = user.FirstName,
                    ResetLink = resetLink
                },
                cancellationToken);

            _logger.LogInformation("Password reset email sent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}", email);
            // Don't throw - user should not know if email sending failed
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<bool> SendAdminResetAsync(
        Guid userId,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FirstAsync(u => u.Id == userId, cancellationToken);

        var lifetime = TimeSpan.FromHours(_emailSettings.AdminPasswordResetTokenExpirationHours);
        // No client address: the request came from the admin, not from the member using the link.
        var token = await IssueTokenAsync(user, lifetime, ipAddress: string.Empty, cancellationToken);

        try
        {
            await _messageService.SendTransactionalAsync(
                user.Email,
                MessageType.AdminPasswordReset,
                new AdminPasswordResetData
                {
                    UserName = user.FirstName,
                    ResetLink = ResetLink(baseUrl, token),
                    ExpiresInHours = (int)lifetime.TotalHours
                },
                cancellationToken);

            _logger.LogInformation("Admin password reset email sent for user {UserId}", user.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send admin password reset email for user {UserId}", user.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ValidateResetTokenResponse> ValidateResetTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(token);

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (resetToken == null)
        {
            return new ValidateResetTokenResponse
            {
                IsValid = false,
                ErrorMessage = "Invalid or expired reset token"
            };
        }

        if (resetToken.IsUsed)
        {
            return new ValidateResetTokenResponse
            {
                IsValid = false,
                ErrorMessage = "This reset link has already been used"
            };
        }

        if (resetToken.IsExpired)
        {
            return new ValidateResetTokenResponse
            {
                IsValid = false,
                ErrorMessage = "This reset link has expired"
            };
        }

        return new ValidateResetTokenResponse
        {
            IsValid = true,
            Email = resetToken.User.Email
        };
    }

    /// <inheritdoc />
    public async Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token);

        var resetToken = await _context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (resetToken == null || !resetToken.IsValid)
        {
            return new ResetPasswordResponse
            {
                Success = false,
                Message = "Invalid or expired reset token"
            };
        }

        if (!PasswordRules.IsAcceptable(request.NewPassword))
        {
            return new ResetPasswordResponse { Success = false, Message = PasswordRules.Message };
        }

        // Update user's password and clear any forced change: they just chose this password
        // themselves, through a link only their mailbox received.
        var user = resetToken.User;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Mark token as used
        resetToken.UsedAt = DateTime.UtcNow;
        resetToken.UpdatedAt = DateTime.UtcNow;

        // Revoke all existing refresh tokens for security
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == user.Id && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        await FeedTokens.RevokeAllForUserAsync(_context, user.Id, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset completed for user {UserId}, IP: {IP}", user.Id, ipAddress);

        // Send confirmation email
        try
        {
            await _messageService.SendTransactionalAsync(
                user.Email,
                MessageType.PasswordChanged,
                new PasswordChangedData { UserName = user.FirstName },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset confirmation email to {Email}", user.Email);
        }

        return new ResetPasswordResponse
        {
            Success = true,
            Message = "Your password has been successfully reset. You can now log in with your new password."
        };
    }

    /// <summary>
    /// Hashes a token using SHA256 for secure storage
    /// </summary>
    /// <summary>
    /// Stores a new single-use reset token for <paramref name="user"/> and returns the raw value,
    /// which exists only in the emailed link. Only its hash is kept.
    /// </summary>
    private async Task<string> IssueTokenAsync(
        User user, TimeSpan lifetime, string ipAddress, CancellationToken cancellationToken)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", ""); // URL-safe base64

        var now = DateTime.UtcNow;
        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = HashToken(token),
            ExpiresAt = now.Add(lifetime),
            IpAddress = ipAddress,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _context.SaveChangesAsync(cancellationToken);

        return token;
    }

    private static string ResetLink(string baseUrl, string token) =>
        $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
