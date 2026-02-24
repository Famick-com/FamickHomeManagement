using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Data;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for WebAuthn/FIDO2 passkey authentication
/// </summary>
public class PasskeyService : IPasskeyService
{
    private readonly HomeManagementDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly IMemoryCache _cache;
    private readonly IFido2 _fido2;
    private readonly PasskeySettings _settings;
    private readonly ILogger<PasskeyService> _logger;

    private const int SessionExpirationMinutes = 5;

    public PasskeyService(
        HomeManagementDbContext context,
        ITokenService tokenService,
        IMapper mapper,
        IConfiguration configuration,
        IContactService contactService,
        IMemoryCache cache,
        IFido2 fido2,
        IOptions<ExternalAuthSettings> settings,
        ILogger<PasskeyService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _contactService = contactService;
        _cache = cache;
        _fido2 = fido2;
        _settings = settings.Value.Passkey;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _settings.IsConfigured;

    /// <inheritdoc />
    public async Task<PasskeyRegisterOptionsResponse> GetRegisterOptionsAsync(
        Guid? userId,
        PasskeyRegisterOptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Passkey authentication is not enabled");
        }

        Fido2User fido2User;
        List<PublicKeyCredentialDescriptor> existingCredentials = [];

        if (userId.HasValue)
        {
            // Existing user adding a passkey
            var user = await _context.Users
                .Include(u => u.PasskeyCredentials)
                .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

            if (user == null)
            {
                throw new EntityNotFoundException("User", userId.Value);
            }

            fido2User = new Fido2User
            {
                Id = user.Id.ToByteArray(),
                Name = user.Email,
                DisplayName = $"{user.FirstName} {user.LastName}".Trim()
            };

            // Exclude existing credentials
            existingCredentials = user.PasskeyCredentials
                .Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.CredentialId)))
                .ToList();
        }
        else
        {
            // New user registration - validate required fields
            if (string.IsNullOrWhiteSpace(request.Email))
            {
                throw new InvalidOperationException("Email is required for new user registration");
            }

            var email = request.Email.ToLower().Trim();

            // Check if email is already in use
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            if (existingUser != null)
            {
                throw new DuplicateEntityException("User", "Email", email);
            }

            // Generate temporary user ID
            var tempUserId = Guid.NewGuid();

            fido2User = new Fido2User
            {
                Id = tempUserId.ToByteArray(),
                Name = email,
                DisplayName = $"{request.FirstName ?? ""} {request.LastName ?? ""}".Trim()
            };
        }

        // Create registration options
        var authenticatorSelection = new AuthenticatorSelection
        {
            UserVerification = _settings.RequireUserVerification
                ? UserVerificationRequirement.Required
                : UserVerificationRequirement.Preferred,
            ResidentKey = ResidentKeyRequirement.Preferred
        };

        var options = _fido2.RequestNewCredential(
            new Fido2NetLib.RequestNewCredentialParams
            {
                User = fido2User,
                ExcludeCredentials = existingCredentials,
                AuthenticatorSelection = authenticatorSelection,
                AttestationPreference = AttestationConveyancePreference.None
            });

        // Store session data
        var sessionId = GenerateSessionId();
        var sessionData = new PasskeyRegistrationSession
        {
            UserId = userId,
            Email = request.Email?.ToLower().Trim(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            DeviceName = request.DeviceName,
            Options = options
        };

        _cache.Set(
            GetRegistrationCacheKey(sessionId),
            sessionData,
            TimeSpan.FromMinutes(SessionExpirationMinutes));

        return new PasskeyRegisterOptionsResponse
        {
            Options = options.ToJson(),
            SessionId = sessionId
        };
    }

    /// <inheritdoc />
    public async Task<PasskeyRegisterVerifyResponse> VerifyRegisterAsync(
        Guid? userId,
        PasskeyRegisterVerifyRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            return new PasskeyRegisterVerifyResponse
            {
                Success = false,
                ErrorMessage = "Passkey authentication is not enabled"
            };
        }

        // Get session data
        var cacheKey = GetRegistrationCacheKey(request.SessionId);
        if (!_cache.TryGetValue<PasskeyRegistrationSession>(cacheKey, out var session))
        {
            return new PasskeyRegisterVerifyResponse
            {
                Success = false,
                ErrorMessage = "Session expired or invalid"
            };
        }

        _cache.Remove(cacheKey);

        try
        {
            // Parse the attestation response
            var attestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(
                request.AttestationResponse);

            if (attestationResponse == null)
            {
                return new PasskeyRegisterVerifyResponse
                {
                    Success = false,
                    ErrorMessage = "Invalid attestation response"
                };
            }

            // Verify the credential
            var result = await _fido2.MakeNewCredentialAsync(
                new MakeNewCredentialParams
                {
                    AttestationResponse = attestationResponse,
                    OriginalOptions = session!.Options,
                    IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                    {
                        // Check if credential ID already exists
                        var credentialIdBase64 = Convert.ToBase64String(args.CredentialId);
                        var existing = await _context.UserPasskeyCredentials
                            .AnyAsync(c => c.CredentialId == credentialIdBase64, ct);
                        return !existing;
                    }
                },
                cancellationToken);

            // Get tenant ID
            var tenantIdString = _configuration["SelfHosted:TenantId"]
                ?? "00000000-0000-0000-0000-000000000001";
            var tenantId = Guid.Parse(tenantIdString);

            User user;
            bool isNewUser = false;

            if (session.UserId.HasValue)
            {
                // Existing user adding passkey
                user = await _context.Users.FindAsync([session.UserId.Value], cancellationToken)
                    ?? throw new EntityNotFoundException("User", session.UserId.Value);
            }
            else
            {
                // Create new user
                isNewUser = true;
                var firstName = session.FirstName ?? session.Email?.Split('@').FirstOrDefault() ?? "User";
                var lastName = session.LastName ?? "";

                user = new User
                {
                    Id = new Guid(session.Options.User.Id),
                    TenantId = tenantId,
                    Email = session.Email!,
                    Username = session.Email!,
                    FirstName = firstName,
                    LastName = lastName,
                    PasswordHash = string.Empty,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.Users.Add(user);

                // Assign role
                var isFirstUser = !await _context.Users.AnyAsync(u => u.Id != user.Id, cancellationToken);
                var role = isFirstUser ? Role.Admin : Role.Viewer;

                var userRole = new UserRole
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UserId = user.Id,
                    Role = role,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.UserRoles.Add(userRole);
                _logger.LogInformation("Created new user {UserId} via passkey with role {Role}", user.Id, role);
            }

            // Store credential (Fido2 v4 returns RegisteredPublicKeyCredential directly)
            var credential = new UserPasskeyCredential
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = user.Id,
                CredentialId = Convert.ToBase64String(result.Id),
                PublicKey = Convert.ToBase64String(result.PublicKey),
                SignatureCounter = result.SignCount,
                DeviceName = request.DeviceName ?? session.DeviceName,
                AaGuid = result.AaGuid.ToString(),
                CredentialType = result.Type.ToString(),
                // UserVerification is determined by our authenticator selection settings
                UserVerification = _settings.RequireUserVerification,
                LastUsedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserPasskeyCredentials.Add(credential);
            await _context.SaveChangesAsync(cancellationToken);

            // Create contact for new user
            if (isNewUser)
            {
                try
                {
                    await _contactService.CreateContactForUserAsync(user, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create contact for user {UserId}", user.Id);
                }
            }

            var response = new PasskeyRegisterVerifyResponse
            {
                Success = true,
                CredentialId = credential.Id
            };

            // Generate tokens for new user
            if (isNewUser)
            {
                var loginResponse = await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, false, cancellationToken);
                response.AccessToken = loginResponse.AccessToken;
                response.RefreshToken = loginResponse.RefreshToken;
                response.ExpiresAt = loginResponse.ExpiresAt;
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying passkey registration");
            return new PasskeyRegisterVerifyResponse
            {
                Success = false,
                ErrorMessage = "Credential verification failed"
            };
        }
    }

    /// <inheritdoc />
    public async Task<PasskeyAuthenticateOptionsResponse> GetAuthenticateOptionsAsync(
        PasskeyAuthenticateOptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Passkey authentication is not enabled");
        }

        List<PublicKeyCredentialDescriptor> allowedCredentials = [];

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            // Get credentials for specific user
            var email = request.Email.ToLower().Trim();
            var user = await _context.Users
                .Include(u => u.PasskeyCredentials)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

            if (user?.PasskeyCredentials.Count > 0)
            {
                allowedCredentials = user.PasskeyCredentials
                    .Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.CredentialId)))
                    .ToList();
            }
        }

        // Create authentication options
        var options = _fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = _settings.RequireUserVerification
                    ? UserVerificationRequirement.Required
                    : UserVerificationRequirement.Preferred
            });

        // Store session
        var sessionId = GenerateSessionId();
        _cache.Set(
            GetAuthenticationCacheKey(sessionId),
            options,
            TimeSpan.FromMinutes(SessionExpirationMinutes));

        return new PasskeyAuthenticateOptionsResponse
        {
            Options = options.ToJson(),
            SessionId = sessionId
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse> VerifyAuthenticateAsync(
        PasskeyAuthenticateVerifyRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Passkey authentication is not enabled");
        }

        // Get session
        var cacheKey = GetAuthenticationCacheKey(request.SessionId);
        if (!_cache.TryGetValue<AssertionOptions>(cacheKey, out var options))
        {
            throw new InvalidCredentialsException("Session expired or invalid");
        }

        _cache.Remove(cacheKey);

        // Parse assertion response
        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            request.AssertionResponse);

        if (assertionResponse == null)
        {
            throw new InvalidCredentialsException("Invalid assertion response");
        }

        // Find credential - assertionResponse.Id is Base64Url encoded in v4
        // Convert from Base64Url to regular Base64 to match our storage format
        var credentialIdBase64 = Convert.ToBase64String(assertionResponse.RawId);
        var credential = await _context.UserPasskeyCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == credentialIdBase64, cancellationToken);

        if (credential == null)
        {
            throw new InvalidCredentialsException("Credential not found");
        }

        // Verify assertion
        var result = await _fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = options!,
                StoredPublicKey = Convert.FromBase64String(credential.PublicKey),
                StoredSignatureCounter = credential.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = async (args, ct) =>
                {
                    // Verify the credential belongs to the user
                    return args.UserHandle.SequenceEqual(credential.User.Id.ToByteArray());
                }
            },
            cancellationToken);

        // In Fido2 v4, MakeAssertionAsync throws exceptions on failure
        // If we reach here, verification was successful

        // Update counter
        credential.SignatureCounter = result.SignCount;
        credential.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} authenticated via passkey", credential.UserId);

        return await GenerateLoginResponseAsync(credential.User, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PasskeyCredentialDto>> GetCredentialsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var credentials = await _context.UserPasskeyCredentials
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return credentials.Select(c => new PasskeyCredentialDto
        {
            Id = c.Id,
            DeviceName = c.DeviceName,
            CreatedAt = c.CreatedAt,
            LastUsedAt = c.LastUsedAt,
            AaGuid = c.AaGuid
        }).ToList();
    }

    /// <inheritdoc />
    public async Task DeleteCredentialAsync(
        Guid userId,
        Guid credentialId,
        CancellationToken cancellationToken = default)
    {
        var credential = await _context.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.UserId == userId, cancellationToken);

        if (credential == null)
        {
            throw new EntityNotFoundException("PasskeyCredential", credentialId);
        }

        // Check that user still has a way to log in
        var user = await _context.Users
            .Include(u => u.ExternalLogins)
            .Include(u => u.PasskeyCredentials)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        var hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
        var hasExternalLogins = user.ExternalLogins.Count > 0;
        var otherPasskeys = user.PasskeyCredentials.Count(c => c.Id != credentialId);

        if (!hasPassword && !hasExternalLogins && otherPasskeys == 0)
        {
            throw new InvalidOperationException("Cannot delete the last authentication method. Please add a password or another login method first.");
        }

        _context.UserPasskeyCredentials.Remove(credential);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted passkey {CredentialId} for user {UserId}", credentialId, userId);
    }

    /// <inheritdoc />
    public async Task RenameCredentialAsync(
        Guid userId,
        Guid credentialId,
        PasskeyRenameRequest request,
        CancellationToken cancellationToken = default)
    {
        var credential = await _context.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.Id == credentialId && c.UserId == userId, cancellationToken);

        if (credential == null)
        {
            throw new EntityNotFoundException("PasskeyCredential", credentialId);
        }

        credential.DeviceName = request.DeviceName;
        credential.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    #region Helper Methods

    private async Task<LoginResponse> GenerateLoginResponseAsync(
        User user,
        string ipAddress,
        string deviceInfo,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        // Load user with permissions and roles
        var loadedUser = await _context.Users
            .Include(u => u.UserPermissions)
                .ThenInclude(up => up.Permission)
            .Include(u => u.UserRoles)
            .FirstAsync(u => u.Id == user.Id, cancellationToken);

        if (!loadedUser.IsActive)
        {
            throw new AccountInactiveException();
        }

        var permissions = loadedUser.UserPermissions.Select(up => up.Permission.Name).ToList();
        var roles = loadedUser.UserRoles.Select(ur => ur.Role).ToList();

        var accessToken = _tokenService.GenerateAccessToken(loadedUser, permissions, roles);
        var accessTokenExpiration = _tokenService.GetTokenExpiration();
        var refreshTokenString = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshTokenString);

        var defaultExpirationDays = _configuration.GetValue("JwtSettings:RefreshTokenExpirationDays", 7);
        var extendedExpirationDays = _configuration.GetValue("JwtSettings:RefreshTokenExtendedExpirationDays", 30);
        var refreshTokenExpirationDays = rememberMe ? extendedExpirationDays : defaultExpirationDays;

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = loadedUser.Id,
            TenantId = loadedUser.TenantId,
            TokenHash = refreshTokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            DeviceInfo = deviceInfo ?? string.Empty,
            IpAddress = ipAddress ?? string.Empty,
            RememberMe = rememberMe,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        loadedUser.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = _mapper.Map<UserDto>(loadedUser);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = accessTokenExpiration,
            User = userDto
        };
    }

    private static string GenerateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string GetRegistrationCacheKey(string sessionId) => $"passkey_register_{sessionId}";
    private static string GetAuthenticationCacheKey(string sessionId) => $"passkey_auth_{sessionId}";

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    #endregion

    #region Helper Classes

    private class PasskeyRegistrationSession
    {
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? DeviceName { get; set; }
        public CredentialCreateOptions Options { get; set; } = null!;
    }

    #endregion
}
