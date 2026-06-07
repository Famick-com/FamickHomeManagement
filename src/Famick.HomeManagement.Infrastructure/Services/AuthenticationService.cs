using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Mapping;
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
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly IMultiTenancyOptions _multiTenancyOptions;
    private readonly IJwtMinIatService _jwtMinIatService;
    private readonly IUserAdvisoryLockService _userLockService;
    private readonly ILocalServerResolver? _localServerResolver;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        HomeManagementDbContext context,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IConfiguration configuration,
        IContactService contactService,
        IJwtMinIatService jwtMinIatService,
        IUserAdvisoryLockService userLockService,
        ILogger<AuthenticationService> logger,
        IMultiTenancyOptions? multiTenancyOptions = null,
        ILocalServerResolver? localServerResolver = null)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _configuration = configuration;
        _contactService = contactService;
        _jwtMinIatService = jwtMinIatService;
        _userLockService = userLockService;
        _multiTenancyOptions = multiTenancyOptions ?? new MultiTenancyOptions { IsMultiTenantEnabled = true };
        _localServerResolver = localServerResolver;
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

        // First user in this tenant becomes Admin. In single-tenant self-hosted
        // this is "first user ever" — the operator who just ran setup.sh. In
        // multi-tenant cloud the same logic applies per tenant: whoever first
        // signs up under a given tenant becomes that tenant's admin. Without
        // this, every fresh install needs a manual SQL UPDATE to unblock the
        // wizard's server-setup step and the admin pages.
        var existingUsersInTenant = await _context.Users
            .IgnoreQueryFilters()
            .CountAsync(u => u.TenantId == tenantId, cancellationToken);
        if (existingUsersInTenant == 1)
        {
            _context.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                TenantId = tenantId,
                Role = Role.Admin,
            });
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "First user in tenant {TenantId} — promoted {Email} to {Role}",
                tenantId, email, Role.Admin);
        }

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

        // First-factor authentication completed now. The auth_time / FamilyId are
        // set once here; rotation copies them forward.
        var loginTime = DateTime.UtcNow;

        // Generate access token. authTime = now (fresh first-factor auth).
        var accessToken = _tokenService.GenerateAccessToken(
            user, permissions, roles, mustAcceptTerms,
            authTime: loginTime,
            iat: loginTime);
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
            // Phase 1 — fresh family per login; AuthTime carries forward via rotation
            // so refreshed access tokens reflect the original authentication time.
            FamilyId = Guid.NewGuid(),
            AuthTime = loginTime,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);

        // Update last login timestamp
        user.LastLoginAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in: {Email}, IP: {IpAddress}", user.Email, ipAddress);

        // Map to DTOs
        var userDto = AuthenticationMapper.ToDto(user);

        // Load tenant for subscription info
        var tenant = await _context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);

        var tenantDto = new TenantInfoDto
        {
            Id = tenant?.Id ?? user.TenantId,
            Name = tenant?.Name ?? string.Empty,
            Subdomain = string.Empty,
        };

        // Self-hosted: all features unlocked (Pro tier)
        // Cloud: populate from tenant entity
        if (!_multiTenancyOptions.IsMultiTenantEnabled)
        {
            tenantDto.SubscriptionTier = "Pro";
            tenantDto.IsTrialActive = false;
            tenantDto.IsExpired = false;
        }
        else if (tenant != null)
        {
            tenantDto.SubscriptionTier = tenant.SubscriptionTier.ToString();
            tenantDto.IsTrialActive = tenant.IsTrialActive;
            tenantDto.TrialEndsAt = tenant.TrialEndsAt;
            tenantDto.IsExpired = tenant.SubscriptionTier == Domain.Enums.SubscriptionTier.Free
                && !tenant.IsTrialActive;
        }

        // Phase 4 chunk 4.D — resolve canonical local-server URL, write audit
        // row on change, persist last-delivered value. Returns null in cloud
        // mode or when MobileAppSetup:PublicUrl is unset on self-hosted.
        var localServer = _localServerResolver is null
            ? null
            : await _localServerResolver.ResolveAndAuditAsync(user, ipAddress, deviceInfo, cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = accessTokenExpiration,
            MustChangePassword = user.MustChangePassword,
            MustAcceptTerms = mustAcceptTerms,
            User = userDto,
            Tenant = tenantDto,
            LocalServer = localServer
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

        if (refreshToken == null)
        {
            _logger.LogWarning("Refresh token not found. IP: {IpAddress}", ipAddress);
            throw new InvalidCredentialsException("Invalid or expired refresh token");
        }

        if (refreshToken.IsExpired)
        {
            _logger.LogWarning("Refresh token expired for user {UserId}, IP: {IpAddress}",
                refreshToken.UserId, ipAddress);
            throw new InvalidCredentialsException("Invalid or expired refresh token");
        }

        // Phase 1 — refresh-token reuse-detection. A revoked-but-presented refresh
        // token is the canonical signal that the family has been compromised:
        // either the legitimate client lost the rotation race against an attacker,
        // or an attacker is replaying a stolen token after the legitimate client
        // already rotated it. Either way, the safe move is to bulk-revoke the
        // entire family and bump the user's jwt_min_iat so the access tokens
        // already issued from this family also fail.
        if (refreshToken.IsRevoked)
        {
            _logger.LogWarning(
                "Refresh-token reuse detected for user {UserId}, family {FamilyId}, IP {IpAddress}. Poisoning family.",
                refreshToken.UserId, refreshToken.FamilyId, ipAddress);

            await _context.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.FamilyId == refreshToken.FamilyId && !rt.IsRevoked)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), cancellationToken);

            var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            await _jwtMinIatService.BumpAsync(refreshToken.UserId, nowSeconds, cancellationToken);

            throw new InvalidCredentialsException("Refresh token reused; session terminated");
        }

        // Phase 1 — wrap the rotation critical section in a per-user advisory lock so
        // a concurrent change-password (which also bumps jwt_min_iat) doesn't race
        // against the rotation. Acquire after the cheap reads / before the writes.
        await using var userLock = await _userLockService.AcquireAsync(
            refreshToken.UserId, TimeSpan.FromSeconds(5), cancellationToken);

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

        // Generate new access token. Preserve auth_time from the parent refresh
        // token so the new JWT reflects the original first-factor authentication
        // time, not the rotation time. This is what makes the (Phase 2) step-up
        // middleware actually meaningful — without preservation, every refresh
        // would silently grant a fresh step-up window.
        var newAccessToken = _tokenService.GenerateAccessToken(
            refreshToken.User, permissions, roles, mustAcceptTerms,
            authTime: refreshToken.AuthTime);
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
            // Phase 1 — inherit family + auth_time from parent. Both stay constant
            // through the descendant chain until a fresh login (new family) or
            // step-up re-auth (Phase 2 — new auth_time).
            FamilyId = refreshToken.FamilyId,
            AuthTime = refreshToken.AuthTime,
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

        // Phase 1 — bump jwt_min_iat so already-issued access tokens for this user
        // are also rejected, not just future ones. Without this, "sign out everywhere"
        // only kills the refresh tokens; access tokens (~60 min lifetime) keep working.
        // Always bump regardless of whether we found refresh tokens to revoke — admin
        // force sign-out and similar paths may want the bump even with no active tokens.
        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _jwtMinIatService.BumpAsync(userId, nowSeconds, cancellationToken);
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
