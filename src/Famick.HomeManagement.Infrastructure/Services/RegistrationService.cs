using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for user registration with email verification (mobile onboarding flow)
/// </summary>
public class RegistrationService : IRegistrationService
{
    private readonly HomeManagementDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegistrationService> _logger;

    // Token expiration times
    private const int VerificationTokenExpirationHours = 24;

    public RegistrationService(
        HomeManagementDbContext context,
        IEmailService emailService,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<RegistrationService> logger)
    {
        _context = context;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<StartRegistrationResponse> StartRegistrationAsync(
        StartRegistrationRequest request,
        string ipAddress,
        string deviceInfo,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        var email = request.Email.ToLower().Trim();
        var householdName = request.HouseholdName.Trim();

        // Check if email already exists as a user
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", email);
            // Return success anyway to prevent email enumeration
            return new StartRegistrationResponse
            {
                Success = true,
                Message = "If this email is not already registered, you will receive a verification email.",
                MaskedEmail = MaskEmail(email)
            };
        }

        // Invalidate any existing pending registrations for this email
        // Note: Using direct column comparison instead of computed property for EF Core translation
        var existingTokens = await _context.EmailVerificationTokens
            .Where(t => t.Email == email && t.CompletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in existingTokens)
        {
            token.CompletedAt = DateTime.UtcNow; // Mark as completed/invalidated
        }

        // Generate verification token
        var verificationToken = GenerateSecureToken();
        var tokenHash = HashToken(verificationToken);

        // Create email verification record
        var emailVerificationToken = new EmailVerificationToken
        {
            Id = Guid.NewGuid(),
            Email = email,
            HouseholdName = householdName,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(VerificationTokenExpirationHours),
            IpAddress = ipAddress,
            DeviceInfo = deviceInfo,
            CreatedAt = DateTime.UtcNow
        };

        _context.EmailVerificationTokens.Add(emailVerificationToken);
        await _context.SaveChangesAsync(cancellationToken);

        // Build verification link (deep link for mobile)
        var verificationLink = $"famick://verify?token={verificationToken}";

        // Also provide a web fallback URL
        var webVerificationLink = $"{baseUrl}/verify-email?token={verificationToken}";

        // Send verification email
        try
        {
            await _emailService.SendEmailVerificationAsync(
                email,
                householdName,
                verificationLink, // Using mobile deep link
                cancellationToken);

            _logger.LogInformation("Verification email sent to {Email} for household {Household}",
                email, householdName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email to {Email}", email);
            throw;
        }

        return new StartRegistrationResponse
        {
            Success = true,
            Message = "Verification email sent. Please check your inbox.",
            MaskedEmail = MaskEmail(email)
        };
    }

    /// <inheritdoc />
    public async Task<VerifyEmailResponse> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token);

        var verificationToken = await _context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (verificationToken == null)
        {
            _logger.LogWarning("Invalid verification token attempted");
            return new VerifyEmailResponse
            {
                Verified = false,
                Message = "Invalid or expired verification link."
            };
        }

        if (verificationToken.IsExpired)
        {
            _logger.LogWarning("Expired verification token for email {Email}", verificationToken.Email);
            return new VerifyEmailResponse
            {
                Verified = false,
                Message = "This verification link has expired. Please start the registration again."
            };
        }

        if (verificationToken.IsCompleted)
        {
            _logger.LogWarning("Already completed verification token for email {Email}", verificationToken.Email);
            return new VerifyEmailResponse
            {
                Verified = false,
                Message = "This verification link has already been used."
            };
        }

        // Mark as verified
        if (!verificationToken.IsVerified)
        {
            verificationToken.VerifiedAt = DateTime.UtcNow;
            verificationToken.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Email verified for {Email}", verificationToken.Email);

        return new VerifyEmailResponse
        {
            Verified = true,
            Email = verificationToken.Email,
            HouseholdName = verificationToken.HouseholdName,
            Message = "Email verified successfully. You can now complete your registration.",
            Token = request.Token // Return the same token for completing registration
        };
    }

    /// <inheritdoc />
    public async Task<CompleteRegistrationResponse> CompleteRegistrationAsync(
        CompleteRegistrationRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.Token);

        var verificationToken = await _context.EmailVerificationTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (verificationToken == null || !verificationToken.IsValidForCompletion)
        {
            var message = verificationToken == null ? "Invalid verification token."
                : verificationToken.IsExpired ? "This verification link has expired."
                : verificationToken.IsCompleted ? "Registration has already been completed."
                : !verificationToken.IsVerified ? "Email has not been verified."
                : "Invalid verification token.";

            _logger.LogWarning("Invalid completion attempt: {Message}", message);

            return new CompleteRegistrationResponse
            {
                Success = false,
                Message = message
            };
        }

        // Validate password (if not using OAuth)
        if (string.IsNullOrEmpty(request.Provider))
        {
            if (string.IsNullOrEmpty(request.Password) || request.Password.Length < 8)
            {
                return new CompleteRegistrationResponse
                {
                    Success = false,
                    Message = "Password must be at least 8 characters."
                };
            }
        }

        // Check if email was taken in the meantime
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == verificationToken.Email, cancellationToken);

        if (existingUser != null)
        {
            verificationToken.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return new CompleteRegistrationResponse
            {
                Success = false,
                Message = "An account with this email already exists."
            };
        }

        // Create new tenant for this household
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = verificationToken.HouseholdName
        };

        _context.Tenants.Add(tenant);

        // Create user
        var currentTermsVersion = _configuration["LegalTerms:CurrentVersion"];
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Email = verificationToken.Email,
            Username = verificationToken.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = string.IsNullOrEmpty(request.Password)
                ? string.Empty // OAuth user - no password
                : _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            // Record terms acceptance at registration (cloud flow)
            TermsAcceptedAt = !string.IsNullOrEmpty(currentTermsVersion) ? DateTime.UtcNow : null,
            TermsVersion = currentTermsVersion,
            TermsAcceptedIpAddress = !string.IsNullOrEmpty(currentTermsVersion) ? ipAddress : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        // Assign Admin role to the first user (household creator)
        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenant.Id,
            Role = Domain.Enums.Role.Admin
        };

        _context.UserRoles.Add(userRole);

        // Create the Home record for this tenant
        var home = new Home
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Homes.Add(home);

        // If OAuth, create external login record
        if (!string.IsNullOrEmpty(request.Provider) && !string.IsNullOrEmpty(request.ProviderToken))
        {
            var externalLogin = new UserExternalLogin
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TenantId = tenant.Id,
                Provider = request.Provider,
                ProviderUserId = request.ProviderToken, // This should be validated with the provider
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserExternalLogins.Add(externalLogin);
        }

        // Mark verification token as completed
        verificationToken.CompletedAt = DateTime.UtcNow;
        verificationToken.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Registration completed for {Email}. TenantId: {TenantId}, UserId: {UserId}",
            user.Email, tenant.Id, user.Id);

        // Generate tokens for immediate login
        // New user is Admin of their household
        var roles = new List<Domain.Enums.Role> { Domain.Enums.Role.Admin };
        var accessToken = _tokenService.GenerateAccessToken(user, new List<string>(), roles);
        var accessTokenExpiration = _tokenService.GetTokenExpiration();
        var refreshTokenString = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshTokenString);

        var refreshTokenExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays", 7);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = tenant.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            DeviceInfo = deviceInfo,
            IpAddress = ipAddress,
            RememberMe = false,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new CompleteRegistrationResponse
        {
            Success = true,
            Message = "Registration completed successfully.",
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = accessTokenExpiration,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Permissions = new List<string>()
            },
            Tenant = new TenantInfoDto
            {
                Id = tenant.Id,
                Name = tenant.Name,
                Subdomain = string.Empty,
                SubscriptionTier = "Free"
            }
        };
    }

    /// <inheritdoc />
    public async Task<StartRegistrationResponse> ResendVerificationEmailAsync(
        string email,
        string baseUrl,
        CancellationToken cancellationToken = default)
    {
        email = email.ToLower().Trim();

        // Find existing pending verification
        // Note: Using direct column comparisons instead of computed properties for EF Core translation
        var now = DateTime.UtcNow;
        var existingToken = await _context.EmailVerificationTokens
            .Where(t => t.Email == email && t.CompletedAt == null && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingToken == null)
        {
            // Return success to prevent email enumeration
            return new StartRegistrationResponse
            {
                Success = true,
                Message = "If a pending registration exists for this email, a new verification email will be sent.",
                MaskedEmail = MaskEmail(email)
            };
        }

        // Generate new verification token
        var verificationToken = GenerateSecureToken();
        var tokenHash = HashToken(verificationToken);

        // Update the existing record with new token
        existingToken.TokenHash = tokenHash;
        existingToken.ExpiresAt = DateTime.UtcNow.AddHours(VerificationTokenExpirationHours);
        existingToken.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Build verification link
        var verificationLink = $"famick://verify?token={verificationToken}";

        // Send verification email
        try
        {
            await _emailService.SendEmailVerificationAsync(
                email,
                existingToken.HouseholdName,
                verificationLink,
                cancellationToken);

            _logger.LogInformation("Verification email resent to {Email}", email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend verification email to {Email}", email);
            throw;
        }

        return new StartRegistrationResponse
        {
            Success = true,
            Message = "Verification email sent. Please check your inbox.",
            MaskedEmail = MaskEmail(email)
        };
    }

    /// <summary>
    /// Generates a cryptographically secure random token
    /// </summary>
    private static string GenerateSecureToken()
    {
        var tokenBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(tokenBytes);
        return Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    /// <summary>
    /// Hashes a token using SHA256 for secure storage
    /// </summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Masks an email address for display (e.g., "j***@example.com")
    /// </summary>
    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***";

        var localPart = parts[0];
        var domain = parts[1];

        var maskedLocal = localPart.Length <= 1
            ? "*"
            : localPart[0] + new string('*', Math.Min(localPart.Length - 1, 5));

        return $"{maskedLocal}@{domain}";
    }
}
