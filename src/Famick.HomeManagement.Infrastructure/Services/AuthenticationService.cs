using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Implementation of authentication and authorization services
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly HomeManagementDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly IMultiTenancyOptions _multiTenancyOptions;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        HomeManagementDbContext context,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper,
        IConfiguration configuration,
        IContactService contactService,
        ILogger<AuthenticationService> logger,
        IMultiTenancyOptions? multiTenancyOptions = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _contactService = contactService;
        _multiTenancyOptions = multiTenancyOptions ?? new MultiTenancyOptions { IsMultiTenantEnabled = true };
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<RegisterResponse> RegisterAsync(
        RegisterRequest request,
        string ipAddress,
        string deviceInfo,
        bool autoLogin = true,
        CancellationToken cancellationToken = default)
    {
        // Normalize email
        var email = request.Email.ToLower().Trim();

        // Check if email already exists
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", email);
            throw new DuplicateEntityException("User", "Email", email);
        }

        // Get the fixed tenant ID for self-hosted
        var tenantIdString = _configuration["SelfHosted:TenantId"]
            ?? "00000000-0000-0000-0000-000000000001";
        var tenantId = Guid.Parse(tenantIdString);

        // Create new user
        var currentTermsVersion = _configuration["LegalTerms:CurrentVersion"];
        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Username = request.Username?.Trim() ?? email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            IsActive = true,
            TermsAcceptedAt = !string.IsNullOrEmpty(currentTermsVersion) ? DateTime.UtcNow : null,
            TermsVersion = currentTermsVersion,
            TermsAcceptedIpAddress = !string.IsNullOrEmpty(currentTermsVersion) ? ipAddress : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New user registered: {Email}, ID: {UserId}", email, user.Id);

        // Create contact record for the user
        try
        {
            await _contactService.CreateContactForUserAsync(user, cancellationToken);
            _logger.LogInformation("Contact created for user: {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create contact for user {UserId}", user.Id);
            // Don't fail registration if contact creation fails
        }

        var response = new RegisterResponse
        {
            UserId = user.Id,
            Email = user.Email,
            Message = "Registration successful. You can now log in."
        };

        // Auto-login if requested
        if (autoLogin)
        {
            var loginRequest = new LoginRequest { Email = email, Password = request.Password };
            var loginResponse = await LoginAsync(loginRequest, ipAddress, deviceInfo, cancellationToken);

            response.AccessToken = loginResponse.AccessToken;
            response.RefreshToken = loginResponse.RefreshToken;
            response.ExpiresAt = loginResponse.ExpiresAt;
            response.Message = "Registration successful. You are now logged in.";
        }

        return response;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        // Find user by email (case-insensitive)
        // IgnoreQueryFilters() is used because login needs to find the user across all tenants
        // to determine which tenant they belong to (tenant context isn't established yet)
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower(), cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        // Load navigation properties separately with filter bypass
        // (IgnoreQueryFilters doesn't propagate to Include)
        await _context.Entry(user)
            .Collection(u => u.UserPermissions)
            .Query()
            .IgnoreQueryFilters()
            .Include(up => up.Permission)
            .LoadAsync(cancellationToken);

        await _context.Entry(user)
            .Collection(u => u.UserRoles)
            .Query()
            .IgnoreQueryFilters()
            .LoadAsync(cancellationToken);

        // Verify password
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for user: {Email}", request.Email);
            throw new InvalidCredentialsException();
        }

        // Check user is active
        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
            throw new AccountInactiveException();
        }

        // Note: Tenant.IsActive check removed - cloud-specific business logic
        // Cloud implementation should override/wrap this method to add tenant checks

        // Get user permissions
        var permissions = user.UserPermissions
            .Select(up => up.Permission.Name)
            .ToList();

        // Get user roles
        var roles = user.UserRoles
            .Select(ur => ur.Role)
            .ToList();

        // Check if user must accept terms (cloud only)
        var mustAcceptTerms = false;
        if (_multiTenancyOptions.IsMultiTenantEnabled)
        {
            var currentVersion = _configuration["LegalTerms:CurrentVersion"];
            if (!string.IsNullOrEmpty(currentVersion))
            {
                mustAcceptTerms = user.TermsAcceptedAt == null || user.TermsVersion != currentVersion;
            }
        }

        // Generate access token
        var accessToken = _tokenService.GenerateAccessToken(user, permissions, roles, mustAcceptTerms);
        var accessTokenExpiration = _tokenService.GetTokenExpiration();

        // Generate refresh token
        var refreshTokenString = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshTokenString);

        // Use longer expiration if "Remember Me" is checked
        var defaultExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays", 7);
        var extendedExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExtendedExpirationDays", 30);
        var refreshTokenExpirationDays = request.RememberMe ? extendedExpirationDays : defaultExpirationDays;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TenantId = user.TenantId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            DeviceInfo = deviceInfo ?? string.Empty,
            IpAddress = ipAddress ?? string.Empty,
            RememberMe = request.RememberMe,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);

        // Update last login timestamp
        user.LastLoginAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in: {Email}, IP: {IpAddress}", user.Email, ipAddress);

        // Map to DTOs
        var userDto = _mapper.Map<UserDto>(user);
        // Note: TenantDto mapping removed - cloud-specific
        // Cloud implementation can extend LoginResponse to include tenant information

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = accessTokenExpiration,
            MustChangePassword = user.MustChangePassword,
            MustAcceptTerms = mustAcceptTerms,
            User = userDto
            // Tenant = tenantDto // Removed - cloud-specific
        };
    }

    /// <inheritdoc />
    public async Task<RefreshTokenResponse> RefreshTokenAsync(
        RefreshTokenRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);

        // Find refresh token (bypass tenant filter since token validates cross-tenant)
        var refreshToken = await _context.RefreshTokens
            .IgnoreQueryFilters()
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            _logger.LogWarning("Refresh token not found or inactive. IP: {IpAddress}", ipAddress);
            throw new InvalidCredentialsException("Invalid or expired refresh token");
        }

        // Load user's permissions and roles separately with filter bypass
        await _context.Entry(refreshToken.User)
            .Collection(u => u.UserPermissions)
            .Query()
            .IgnoreQueryFilters()
            .Include(up => up.Permission)
            .LoadAsync(cancellationToken);

        await _context.Entry(refreshToken.User)
            .Collection(u => u.UserRoles)
            .Query()
            .IgnoreQueryFilters()
            .LoadAsync(cancellationToken);

        // Check user is still active
        if (!refreshToken.User.IsActive)
        {
            throw new AccountInactiveException();
        }

        // Note: Tenant.IsActive check removed - cloud-specific business logic

        // Get user permissions
        var permissions = refreshToken.User.UserPermissions
            .Select(up => up.Permission.Name)
            .ToList();

        // Get user roles
        var roles = refreshToken.User.UserRoles
            .Select(ur => ur.Role)
            .ToList();

        // Check if user must accept terms (cloud only)
        var mustAcceptTerms = false;
        if (_multiTenancyOptions.IsMultiTenantEnabled)
        {
            var currentVersion = _configuration["LegalTerms:CurrentVersion"];
            if (!string.IsNullOrEmpty(currentVersion))
            {
                mustAcceptTerms = refreshToken.User.TermsAcceptedAt == null || refreshToken.User.TermsVersion != currentVersion;
            }
        }

        // Generate new access token
        var newAccessToken = _tokenService.GenerateAccessToken(refreshToken.User, permissions, roles, mustAcceptTerms);
        var newAccessTokenExpiration = _tokenService.GetTokenExpiration();

        // Generate new refresh token (rotation)
        var newRefreshTokenString = _tokenService.GenerateRefreshToken();
        var newRefreshTokenHash = HashToken(newRefreshTokenString);

        // Preserve the "Remember Me" preference from the original token
        var defaultExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExpirationDays", 7);
        var extendedExpirationDays = _configuration.GetValue<int>("JwtSettings:RefreshTokenExtendedExpirationDays", 30);
        var refreshTokenExpirationDays = refreshToken.RememberMe ? extendedExpirationDays : defaultExpirationDays;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = refreshToken.UserId,
            TenantId = refreshToken.TenantId,
            TokenHash = newRefreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            DeviceInfo = deviceInfo ?? string.Empty,
            IpAddress = ipAddress ?? string.Empty,
            RememberMe = refreshToken.RememberMe,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(newRefreshToken);

        // Revoke old refresh token
        refreshToken.IsRevoked = true;
        refreshToken.RevokedAt = DateTime.UtcNow;
        refreshToken.ReplacedByTokenId = newRefreshToken.Id;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Token refreshed for user: {UserId}, IP: {IpAddress}",
            refreshToken.UserId, ipAddress);

        return new RefreshTokenResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenString,
            ExpiresAt = newAccessTokenExpiration
        };
    }

    /// <inheritdoc />
    public async Task RevokeRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(refreshToken);

        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (token != null && !token.IsRevoked)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Refresh token revoked for user: {UserId}", token.UserId);
        }
    }

    /// <inheritdoc />
    public async Task RevokeAllUserTokensAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = DateTime.UtcNow;
        }

        if (activeTokens.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("All refresh tokens revoked for user: {UserId}, Count: {Count}",
                userId, activeTokens.Count);
        }
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
