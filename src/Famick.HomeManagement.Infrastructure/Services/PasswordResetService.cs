using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Interfaces;
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
    private readonly IPasswordHasher _passwordHasher;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<PasswordResetService> _logger;

    // Rate limiting: max 3 requests per email per hour
    private const int MaxRequestsPerHour = 3;

    public PasswordResetService(
        HomeManagementDbContext context,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        IOptions<EmailSettings> emailSettings,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _emailService = emailService;
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
        var email = request.Email.ToLower().Trim();

        // Always return success message (prevent email enumeration)
        var response = new ForgotPasswordResponse();

        // Find user by email
        var user = await _context.Users
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

        // Generate secure token
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", ""); // URL-safe base64

        var tokenHash = HashToken(token);

        // Create reset token entity
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_emailSettings.PasswordResetTokenExpirationMinutes),
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PasswordResetTokens.Add(resetToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Build reset link
        var resetLink = $"{baseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

        // Send email (fire and forget with error handling)
        try
        {
            await _emailService.SendPasswordResetEmailAsync(
                user.Email,
                user.FirstName,
                resetLink,
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

        // Update user's password
        var user = resetToken.User;
        user.PasswordHash = _passwordHasher.HashPassword(request.NewPassword);
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

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset completed for user {UserId}, IP: {IP}", user.Id, ipAddress);

        // Send confirmation email
        try
        {
            await _emailService.SendPasswordResetConfirmationEmailAsync(
                user.Email,
                user.FirstName,
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
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
