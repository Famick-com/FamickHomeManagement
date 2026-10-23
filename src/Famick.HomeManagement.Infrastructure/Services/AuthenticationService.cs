using Famick.HomeManagement.Core.Helpers;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Mapping;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Validators.Common;
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
public class AuthenticationService : IAuthenticationService, IHaIngressSessionIssuer
{
    /// <summary>
    /// How long a just-rotated refresh token may still be exchanged, for concurrent refreshes
    /// that presented the same token. See <see cref="RefreshTokenAsync"/>.
    /// </summary>
    public static readonly TimeSpan RotationGracePeriod = TimeSpan.FromSeconds(10);

    private readonly HomeManagementDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly IMultiTenancyOptions _multiTenancyOptions;
    private readonly IJwtMinIatService _jwtMinIatService;
    private readonly ILoginThrottle _loginThrottle;
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
        ILoginThrottle loginThrottle,
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
        _loginThrottle = loginThrottle;
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
        var email = EmailAddress.Normalize(request.Email);

        // Check if email already exists
        var existingUser = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser != null)
        {
            _logger.LogWarning("Registration attempt with existing email: {Email}", email);
            throw new DuplicateEntityException("User", "Email", email);
        }

        // The fixed tenant for self-hosted, with no fallback. This method is only reachable at
        // first run — AuthApiController.Register refuses once ISetupService.HasUsersAsync is true
        // — so on any working host the key is configured by the time it matters: every shipped
        // deployment asset sets SelfHosted__TenantId, and Program.cs asserts in Production that
        // it agrees with FixedTenantId. Defaulting instead wrote the household into a tenant
        // nothing else resolves to, so the account would read as empty forever rather than fail.
        // The Home Assistant add-on makes that concrete: it generates a random per-install
        // tenant UUID, so the old fallback was wrong there too, not only on cloud.
        if (!Guid.TryParse(_configuration["SelfHosted:TenantId"], out var tenantId))
        {
            throw new InvalidOperationException(
                "SelfHosted:TenantId is not configured, so a new user cannot be assigned to " +
                "a household. Set it to the same value as FixedTenantId.");
        }

        PasswordRules.Ensure(request.Password, nameof(request.Password));

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
        // Locked accounts are refused before any lookup or bcrypt, which also keeps a login flood
        // from turning into a CPU flood. The key is the email as typed, so an address with no
        // account locks and answers exactly like one that has an account.
        var throttleKey = LoginThrottleKeys.Password(request.Email);
        if (await _loginThrottle.GetLockoutAsync(throttleKey, cancellationToken) is { } lockout)
        {
            _logger.LogWarning("Login refused while locked out");
            throw new TooManyAttemptsException(lockout);
        }

        // Find user by email (case-insensitive)
        // IgnoreQueryFilters() is used because login needs to find the user across all tenants
        // to determine which tenant they belong to (tenant context isn't established yet)
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == EmailAddress.Normalize(request.Email), cancellationToken);

        // Every failure pays one bcrypt verify — against a throwaway hash when there is no account
        // or no password — so the response time does not say whether the email is registered.
        var hasPassword = user is not null && !string.IsNullOrEmpty(user.PasswordHash);
        var passwordMatches = _passwordHasher.VerifyPassword(
            request.Password,
            hasPassword ? user!.PasswordHash : DummyPasswordHash(_passwordHasher));

        if (!hasPassword || !passwordMatches)
        {
            _logger.LogWarning("Failed login attempt for {Email}", request.Email);
            await _loginThrottle.RecordFailureAsync(throttleKey, cancellationToken);
            throw new InvalidCredentialsException();
        }

        await _loginThrottle.RecordSuccessAsync(throttleKey, cancellationToken);

        // Load navigation properties separately with filter bypass
        // (IgnoreQueryFilters doesn't propagate to Include)
        await _context.Entry(user!)
            .Collection(u => u.UserPermissions)
            .Query()
            .IgnoreQueryFilters()
            .Include(up => up.Permission)
            .LoadAsync(cancellationToken);

        await _context.Entry(user!)
            .Collection(u => u.UserRoles)
            .Query()
            .IgnoreQueryFilters()
            .LoadAsync(cancellationToken);

        // Check user is active
        if (!user!.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user: {Email}", request.Email);
            throw new AccountInactiveException();
        }

        // Note: Tenant.IsActive check removed - cloud-specific business logic
        // Cloud implementation should override/wrap this method to add tenant checks

        return await BuildSessionAsync(user, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    private static readonly ConditionalWeakTable<IPasswordHasher, string> DummyHashes = new();

    /// <summary>
    /// A hash of a random password, made once per hasher so it carries the configured work
    /// factor and a failed lookup costs what a wrong password does.
    /// </summary>
    internal static string DummyPasswordHash(IPasswordHasher hasher) =>
        DummyHashes.GetValue(hasher, h =>
            h.HashPassword(Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))) ?? string.Empty);

    /// <summary>
    /// Issues access + refresh tokens and assembles the <see cref="LoginResponse"/>
    /// for an already-authenticated, active user whose roles/permissions are
    /// loaded. Shared by password <see cref="LoginAsync"/> and the HA Ingress
    /// <see cref="IssueSessionAsync"/> path, so token issuance lives in one place.
    /// </summary>
    private async Task<LoginResponse> BuildSessionAsync(
        User user,
        string ipAddress,
        string deviceInfo,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        // Get user permissions
        var permissions = user.UserPermissions
            .Select(up => up.Permission.Name)
            .ToList();

        // Get user roles
        var roles = user.UserRoles
            .Select(ur => ur.Role)
            .ToList();

        var mustAcceptTerms = LoginResponseHelper.MustAcceptTerms(user, _multiTenancyOptions, _configuration);

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
        var refreshTokenExpirationDays = rememberMe ? extendedExpirationDays : defaultExpirationDays;

        // Fresh family per login; AuthTime carries forward via rotation so
        // refreshed access tokens reflect the original authentication time.
        var refreshToken = RefreshToken.StartFamily(
            user.Id, user.TenantId, refreshTokenHash,
            expiresAt: DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            authTime: loginTime,
            deviceInfo, ipAddress, rememberMe);

        _context.RefreshTokens.Add(refreshToken);

        // Update last login timestamp
        user.LastLoginAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User logged in: {Email}, IP: {IpAddress}", user.Email, ipAddress);

        // Map to DTOs
        var userDto = AuthenticationMapper.ToDto(user);

        var tenantDto = await LoginResponseHelper.BuildTenantInfoAsync(_context, user, _multiTenancyOptions, cancellationToken);

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
    public async Task<LoginResponse> IssueSessionAsync(
        Guid userId,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        // Identity has already been proven out-of-band (HA Ingress trusted
        // headers + trusted-proxy gate). Load the user across tenants, then
        // pull roles/permissions the same way LoginAsync does.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new InvalidCredentialsException();
        }

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

        if (!user.IsActive)
        {
            throw new AccountInactiveException();
        }

        // No "remember me" for an SSO session — use the default refresh lifetime.
        return await BuildSessionAsync(user, ipAddress, deviceInfo, rememberMe: false, cancellationToken);
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
        // A token rotated moments ago is usually not theft. Two tabs restored at once, or two
        // requests from one tab, each refresh with the same cookie, and the slower one presents
        // the token the faster one has just rotated. Inside RotationGracePeriod that is let
        // through as a sibling in the same family, provided the family is still live. A token
        // revoked any other way (logout, a poisoned family) has no ReplacedByTokenId and gets no
        // grace, and a replay after the window still poisons the family below.
        var concurrentRotation = refreshToken.IsRevoked
            && refreshToken.ReplacedByTokenId != null
            && refreshToken.RevokedAt >= DateTime.UtcNow - RotationGracePeriod
            && await _context.RefreshTokens
                .IgnoreQueryFilters()
                .AnyAsync(rt => rt.UserId == refreshToken.UserId
                    && rt.FamilyId == refreshToken.FamilyId
                    && !rt.IsRevoked
                    && rt.ExpiresAt > DateTime.UtcNow, cancellationToken);

        if (concurrentRotation)
        {
            _logger.LogInformation(
                "Refresh token for user {UserId}, family {FamilyId} presented again within the rotation grace period; issuing a sibling.",
                refreshToken.UserId, refreshToken.FamilyId);
        }
        else if (refreshToken.IsRevoked)
        {
            _logger.LogWarning(
                "Refresh-token reuse detected for user {UserId}, family {FamilyId}, IP {IpAddress}. Poisoning family.",
                refreshToken.UserId, refreshToken.FamilyId, ipAddress);

            // The family is scoped to its user as well. A family id is meant to be
            // unique, but one shared across users (as Guid.Empty once was, issue #106)
            // must never let one person's replay end anyone else's sessions. An empty
            // family is no family at all, so it falls back to all of this user's
            // sessions, which is what the jwt_min_iat bump below does to their access
            // tokens anyway.
            var familyId = refreshToken.FamilyId;
            var userId = refreshToken.UserId;
            await _context.RefreshTokens
                .IgnoreQueryFilters()
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .Where(rt => familyId == Guid.Empty || rt.FamilyId == familyId)
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

        var mustAcceptTerms = LoginResponseHelper.MustAcceptTerms(refreshToken.User, _multiTenancyOptions, _configuration);

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

        // Revoke old refresh token. A concurrent rotation's parent is already revoked, and it keeps
        // the RevokedAt that started its grace period, so the window cannot be stretched.
        if (!concurrentRotation)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = DateTime.UtcNow;
            refreshToken.ReplacedByTokenId = newRefreshToken.Id;
        }

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
