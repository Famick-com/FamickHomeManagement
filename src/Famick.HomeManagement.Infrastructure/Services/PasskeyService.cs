using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Famick.HomeManagement.Core.Configuration;
using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Core.DTOs.ExternalAuth;
using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Core.Mapping;
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
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly ISetupService _setupService;
    private readonly IMemoryCache _cache;
    private readonly IFido2 _fido2;
    private readonly PasskeySettings _settings;
    private readonly ILocalServerResolver? _localServerResolver;
    private readonly ILogger<PasskeyService> _logger;

    private const int SessionExpirationMinutes = 5;

    public PasskeyService(
        HomeManagementDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        IContactService contactService,
        ISetupService setupService,
        IMemoryCache cache,
        IFido2 fido2,
        IOptions<ExternalAuthSettings> settings,
        ILogger<PasskeyService> logger,
        ILocalServerResolver? localServerResolver = null)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _contactService = contactService;
        _setupService = setupService;
        _cache = cache;
        _fido2 = fido2;
        _settings = settings.Value.Passkey;
        _localServerResolver = localServerResolver;
        _logger = logger;

        _nativeFido2 = new Lazy<IFido2>(() => new Fido2(new Fido2Configuration
        {
            ServerDomain = _settings.NativeRelyingPartyId,
            ServerName = _settings.RelyingPartyName,
            Origins = _settings.NativeOrigins.ToHashSet(),
            Timeout = _settings.Timeout,
        }));
    }

    /// <summary>
    /// Picks the relying-party configuration for a ceremony.
    /// </summary>
    /// <remarks>
    /// Both halves of a ceremony must use the same one: the options call stamps the RP ID into the
    /// challenge the authenticator signs over, and verification compares the assertion's RP ID hash
    /// and origin against the configuration it is handed. Mixing them fails as a generic
    /// verification error that reads like a bad signature, which is why the choice is persisted in
    /// the session rather than re-derived at verify time.
    /// </remarks>
    private IFido2 ResolveFido2(bool native) =>
        native && _settings.IsNativeConfigured ? _nativeFido2.Value : _fido2;

    /// <summary>
    /// The RP ID a ceremony will run under, recorded on the credential so a later ceremony can tell
    /// which configuration a stored credential belongs to.
    /// </summary>
    private string ResolveRelyingPartyId(bool native) =>
        native && _settings.IsNativeConfigured
            ? _settings.NativeRelyingPartyId
            : _settings.RelyingPartyId;

    /// <summary>
    /// Refuses the anonymous (new-user) passkey registration branch once the server holds users.
    /// </summary>
    /// <remarks>
    /// This is the same gate <c>AuthApiController.Register</c> applies to the password path, and it
    /// was the only unauthenticated account-creation path without it. Left open, a caller who can
    /// reach an origin matching the relying party — any page on the cloud app's own domain, not just
    /// the mobile app — could create an account with no email verification, no terms consent and no
    /// household provisioning. On a multi-tenant host the new rows were additionally stamped with
    /// the single-tenant fallback tenant id, so the account landed in a household that was not the
    /// caller's.
    ///
    /// <see cref="ISetupService.HasUsersAsync"/> is the right signal because it runs with
    /// <c>IgnoreQueryFilters()</c>: its answer does not depend on tenant resolution, which is
    /// precisely what an unauthenticated request cannot supply. The first-run case — standing up a
    /// fresh self-hosted server with a passkey instead of a password — still works, because that is
    /// the one time no users exist.
    /// </remarks>
    private async Task EnsureAnonymousRegistrationAllowedAsync(CancellationToken cancellationToken)
    {
        if (await _setupService.HasUsersAsync(cancellationToken))
        {
            _logger.LogWarning("Anonymous passkey registration refused — the server already has users");
            throw new RegistrationClosedException(
                "Registration is closed. The system has already been set up.");
        }
    }

    /// <inheritdoc />
    public bool IsEnabled => _settings.IsConfigured;

    /// <inheritdoc />
    public bool IsNativeEnabled => _settings.IsNativeConfigured;

    /// <summary>
    /// The <see cref="IFido2"/> for a native-app ceremony, built from
    /// <c>PasskeySettings.NativeRelyingPartyId</c> and <c>NativeOrigins</c>.
    /// </summary>
    /// <remarks>
    /// Built here rather than registered in DI because it is the second of two configurations and
    /// only the injected one is the host's "real" relying party. Lazy because a deployment with
    /// native support switched off should not pay for it, and <see cref="Lazy{T}"/> rather than a
    /// null-coalescing field because the service is scoped and this keeps construction single even
    /// if two ceremonies interleave.
    ///
    /// No <c>IMetadataService</c> is passed. Attestation is requested as
    /// <see cref="AttestationConveyancePreference.None"/>, so no authenticator metadata is consulted
    /// and the injected instance's metadata service — when it has one — is not needed here either.
    /// </remarks>
    private readonly Lazy<IFido2> _nativeFido2;

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

        var native = PasskeyClientType.IsNative(request.ClientType);
        var relyingPartyId = ResolveRelyingPartyId(native);

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

            // Exclude existing credentials, but only those registered under the RP ID this ceremony
            // runs under. A credential from the other configuration cannot be asserted here, so
            // excluding it would block the user from registering a usable one — and on Android it
            // would also put unusable entries in the system passkey sheet. Rows predating the
            // RelyingPartyId column are null and treated as the web RP ID. On a deployment where
            // both RP IDs are the same string (the cloud app) this filters nothing.
            existingCredentials = user.PasskeyCredentials
                .Where(c => CredentialMatchesRelyingParty(c, relyingPartyId))
                .Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.CredentialId)))
                .ToList();
        }
        else
        {
            // New user registration. Refused once the server has users — see
            // EnsureAnonymousRegistrationAllowedAsync. Checked before the email is even looked at,
            // so the duplicate-email 409 below cannot be used as an account-existence oracle by an
            // unauthenticated caller.
            await EnsureAnonymousRegistrationAllowedAsync(cancellationToken);

            // Validate required fields
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

        var options = ResolveFido2(native).RequestNewCredential(
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
            IsNative = native,
            RelyingPartyId = relyingPartyId,
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

        // Re-check the gate rather than trusting that options were refused. Sessions live
        // SessionExpirationMinutes in the cache, so one minted before this code deployed must not
        // stay redeemable after it; and the options and verify calls are separate requests, so
        // nothing carries the earlier decision forward.
        if (!session!.UserId.HasValue)
        {
            await EnsureAnonymousRegistrationAllowedAsync(cancellationToken);
        }

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
            // The session's choice, not the current request's — verify must use the configuration
            // the options were built with.
            var result = await ResolveFido2(session.IsNative).MakeNewCredentialAsync(
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

            User user;
            Guid tenantId;
            bool isNewUser = false;

            if (session.UserId.HasValue)
            {
                // Existing user adding passkey. The tenant comes from the user, not from
                // configuration: SelfHosted:TenantId is unset on a multi-tenant host, so reading it
                // here stamped every cloud user's credential with the single-tenant fallback id
                // instead of their own household.
                user = await _context.Users.FindAsync([session.UserId.Value], cancellationToken)
                    ?? throw new EntityNotFoundException("User", session.UserId.Value);
                tenantId = user.TenantId;
            }
            else
            {
                // First-run registration only, per the gate above, which is why configuration is a
                // sound source here. No silent fallback: a host that failed to set this would
                // otherwise write the household into a tenant nothing else resolves to, and the
                // account would read as empty forever rather than fail. Program.cs already asserts
                // this agrees with FixedTenantId at startup.
                if (!Guid.TryParse(_configuration["SelfHosted:TenantId"], out tenantId))
                {
                    throw new InvalidOperationException(
                        "SelfHosted:TenantId is not configured, so a new user cannot be assigned to " +
                        "a household. Set it to the same value as FixedTenantId.");
                }

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
                // Which relying party this credential belongs to. Needed on a server whose web and
                // native RP IDs differ, so later ceremonies can filter to credentials they can
                // actually assert.
                RelyingPartyId = session.RelyingPartyId,
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
    public Task<PasskeyRegisterOptionsResponse> CreatePendingSignupOptionsAsync(
        string email,
        string? displayName,
        string? deviceName,
        bool native,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Passkey authentication is not enabled");
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("A verified email is required to create a passkey");
        }

        var relyingPartyId = ResolveRelyingPartyId(native);

        // The handle minted here becomes the new user's id, so the caller cannot choose it. There is
        // no account yet, hence no credentials to exclude.
        var userHandle = Guid.NewGuid();
        var normalizedEmail = email.ToLower().Trim();

        var options = ResolveFido2(native).RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = new Fido2User
                {
                    Id = userHandle.ToByteArray(),
                    Name = normalizedEmail,
                    DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedEmail : displayName,
                },
                ExcludeCredentials = [],
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    UserVerification = _settings.RequireUserVerification
                        ? UserVerificationRequirement.Required
                        : UserVerificationRequirement.Preferred,
                    // Required rather than Preferred, unlike the other registration path. An account
                    // whose only credential is non-discoverable could not be used for the
                    // usernameless sign-in this flow exists to enable, and the user would have no
                    // password to fall back on.
                    ResidentKey = ResidentKeyRequirement.Required,
                },
                AttestationPreference = AttestationConveyancePreference.None,
            });

        var sessionId = GenerateSessionId();
        _cache.Set(
            GetPendingSignupCacheKey(sessionId),
            new PasskeyPendingSignupSession
            {
                Email = normalizedEmail,
                DeviceName = deviceName,
                IsNative = native,
                RelyingPartyId = relyingPartyId,
                UserHandle = userHandle,
                Options = options,
            },
            TimeSpan.FromMinutes(SessionExpirationMinutes));

        _logger.LogInformation("Issued pending-signup passkey options for a verified registration");

        return Task.FromResult(new PasskeyRegisterOptionsResponse
        {
            Options = options.ToJson(),
            SessionId = sessionId,
        });
    }

    /// <inheritdoc />
    public async Task<PasskeyVerifiedCredential> VerifyPendingSignupAsync(
        string sessionId,
        string attestationResponse,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Passkey authentication is not enabled");
        }

        var cacheKey = GetPendingSignupCacheKey(sessionId);
        if (!_cache.TryGetValue<PasskeyPendingSignupSession>(cacheKey, out var session))
        {
            throw new InvalidOperationException("Passkey session expired or invalid");
        }

        // Single use. Removed before verification so a failed attempt cannot be replayed against the
        // same challenge.
        _cache.Remove(cacheKey);

        var parsed = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(attestationResponse)
            ?? throw new InvalidOperationException("Invalid attestation response");

        var result = await ResolveFido2(session!.IsNative).MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = parsed,
                OriginalOptions = session.Options,
                IsCredentialIdUniqueToUserCallback = async (args, ct) =>
                {
                    var credentialIdBase64 = Convert.ToBase64String(args.CredentialId);
                    // IgnoreQueryFilters: this runs before the user exists, so no tenant is
                    // resolvable, and uniqueness has to hold across the whole server regardless.
                    var existing = await _context.UserPasskeyCredentials
                        .IgnoreQueryFilters()
                        .AnyAsync(c => c.CredentialId == credentialIdBase64, ct);
                    return !existing;
                },
            },
            cancellationToken);

        return new PasskeyVerifiedCredential
        {
            UserHandle = session.UserHandle,
            CredentialId = Convert.ToBase64String(result.Id),
            PublicKey = Convert.ToBase64String(result.PublicKey),
            SignatureCounter = result.SignCount,
            AaGuid = result.AaGuid.ToString(),
            CredentialType = result.Type.ToString(),
            RelyingPartyId = session.RelyingPartyId,
            UserVerification = _settings.RequireUserVerification,
            DeviceName = session.DeviceName,
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse> IssuePendingSignupSessionAsync(
        Guid userId,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters: the user was created moments ago on a request with no tenant context,
        // so the global filter would not find them.
        var user = await _context.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new EntityNotFoundException("User", userId);

        return await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, rememberMe: false, cancellationToken);
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

        var native = PasskeyClientType.IsNative(request.ClientType);
        var relyingPartyId = ResolveRelyingPartyId(native);

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
                // Scoped to the RP ID this ceremony runs under — offering a credential from the
                // other configuration would surface a passkey in the OS sheet that cannot verify
                // here. No-op where both RP IDs are the same string.
                allowedCredentials = user.PasskeyCredentials
                    .Where(c => CredentialMatchesRelyingParty(c, relyingPartyId))
                    .Select(c => new PublicKeyCredentialDescriptor(Convert.FromBase64String(c.CredentialId)))
                    .ToList();
            }
        }

        // Create authentication options
        var options = ResolveFido2(native).GetAssertionOptions(
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
            new PasskeyAssertionSession { IsNative = native, Options = options },
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
        if (!_cache.TryGetValue<PasskeyAssertionSession>(cacheKey, out var session))
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
        var result = await ResolveFido2(session!.IsNative).MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = session!.Options,
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
    public async Task VerifyReauthAssertionAsync(
        Guid userId,
        PasskeyAuthenticateVerifyRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Passkey authentication is not enabled");
        }

        var cacheKey = GetAuthenticationCacheKey(request.SessionId);
        if (!_cache.TryGetValue<PasskeyAssertionSession>(cacheKey, out var session))
        {
            throw new InvalidCredentialsException("Session expired or invalid");
        }

        _cache.Remove(cacheKey);

        var assertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(
            request.AssertionResponse);

        if (assertionResponse == null)
        {
            throw new InvalidCredentialsException("Invalid assertion response");
        }

        // Look up by credential ID (same logic as VerifyAuthenticateAsync), then
        // explicitly check that the credential belongs to the caller. Without
        // this binding the endpoint would let any user reauth as themselves
        // using ANY registered passkey on the device — defeats the point of
        // step-up since the OS sheet might surface a sibling account's passkey.
        var credentialIdBase64 = Convert.ToBase64String(assertionResponse.RawId);
        var credential = await _context.UserPasskeyCredentials
            .FirstOrDefaultAsync(c => c.CredentialId == credentialIdBase64, cancellationToken);

        if (credential == null || credential.UserId != userId)
        {
            throw new InvalidCredentialsException("Credential not found");
        }

        // MakeAssertionAsync throws on invalid signature / replayed counter /
        // wrong origin / etc. Reaching the line after means verification passed.
        var result = await ResolveFido2(session!.IsNative).MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = session!.Options,
                StoredPublicKey = Convert.FromBase64String(credential.PublicKey),
                StoredSignatureCounter = credential.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = (args, ct) =>
                    Task.FromResult(args.UserHandle.SequenceEqual(userId.ToByteArray()))
            },
            cancellationToken);

        credential.SignatureCounter = result.SignCount;
        credential.LastUsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} reauthenticated via passkey", userId);
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

        var userDto = AuthenticationMapper.ToDto(loadedUser);

        // Phase 4 chunk 4.D — same local-server hydration as password / social.
        var localServer = _localServerResolver is null
            ? null
            : await _localServerResolver.ResolveAndAuditAsync(loadedUser, ipAddress: null, userAgent: null, cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresAt = accessTokenExpiration,
            User = userDto,
            LocalServer = localServer
        };
    }

    private static string GenerateSessionId()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string GetRegistrationCacheKey(string sessionId) => $"passkey_register_{sessionId}";
    private static string GetAuthenticationCacheKey(string sessionId) => $"passkey_auth_{sessionId}";

    /// <summary>
    /// Cache key for a pending-signup ceremony. A distinct prefix is the mechanism that keeps these
    /// sessions out of <see cref="VerifyRegisterAsync"/>: that method looks under
    /// <see cref="GetRegistrationCacheKey"/> and will never find one, so a session minted for a
    /// token-verified signup cannot be redeemed down the path that creates a user by itself.
    /// </summary>
    private static string GetPendingSignupCacheKey(string sessionId) => $"passkey_signup_{sessionId}";

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

        /// <summary>
        /// Whether the options were built with the native relying-party configuration. Carried
        /// through so verify cannot pick the other one — see <c>ResolveFido2</c>.
        /// </summary>
        public bool IsNative { get; set; }

        /// <summary>The RP ID the options were built with, stored on the resulting credential.</summary>
        public string RelyingPartyId { get; set; } = string.Empty;

        public CredentialCreateOptions Options { get; set; } = null!;
    }

    /// <summary>
    /// Assertion-ceremony session. Previously the <see cref="AssertionOptions"/> were cached bare;
    /// they are wrapped now so the relying-party choice survives to the verify call, which is a
    /// separate request.
    /// </summary>
    private class PasskeyAssertionSession
    {
        public bool IsNative { get; set; }
        public AssertionOptions Options { get; set; } = null!;
    }

    /// <summary>
    /// A creation ceremony for an account that does not exist yet, authorized by the caller having
    /// validated a verified registration token. Held under its own cache key so it cannot be
    /// redeemed through <see cref="VerifyRegisterAsync"/> — see
    /// <see cref="GetPendingSignupCacheKey"/>.
    /// </summary>
    private class PasskeyPendingSignupSession
    {
        public string Email { get; set; } = string.Empty;
        public string? DeviceName { get; set; }
        public bool IsNative { get; set; }
        public string RelyingPartyId { get; set; } = string.Empty;

        /// <summary>
        /// Minted server-side, returned to the caller as the id the new user must be created with.
        /// </summary>
        public Guid UserHandle { get; set; }

        public CredentialCreateOptions Options { get; set; } = null!;
    }

    /// <summary>
    /// Whether a stored credential belongs to the relying party a ceremony is running under.
    /// </summary>
    /// <remarks>
    /// A null <see cref="UserPasskeyCredential.RelyingPartyId"/> means the column predates this
    /// feature, so the credential was necessarily created through the browser and belongs to the web
    /// RP ID. Comparing ordinal-ignore-case because an RP ID is a domain name.
    /// </remarks>
    private bool CredentialMatchesRelyingParty(UserPasskeyCredential credential, string relyingPartyId) =>
        string.Equals(
            credential.RelyingPartyId ?? _settings.RelyingPartyId,
            relyingPartyId,
            StringComparison.OrdinalIgnoreCase);

    #endregion
}
