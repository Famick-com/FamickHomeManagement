using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for handling external authentication providers (Google, Apple, OIDC)
/// </summary>
public class ExternalAuthService : IExternalAuthService
{
    private readonly HomeManagementDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalAuthSettings _settings;
    private readonly ILocalServerResolver? _localServerResolver;
    private readonly ILogger<ExternalAuthService> _logger;

    private const int StateExpirationMinutes = 10;

    public ExternalAuthService(
        HomeManagementDbContext context,
        ITokenService tokenService,
        IConfiguration configuration,
        IContactService contactService,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        IOptions<ExternalAuthSettings> settings,
        ILogger<ExternalAuthService> logger,
        ILocalServerResolver? localServerResolver = null)
    {
        _context = context;
        _tokenService = tokenService;
        _configuration = configuration;
        _contactService = contactService;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _localServerResolver = localServerResolver;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<ExternalAuthProviderDto>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default)
    {
        var providers = new List<ExternalAuthProviderDto>();

        if (_settings.Google.IsConfigured)
        {
            providers.Add(new ExternalAuthProviderDto
            {
                Provider = "Google",
                DisplayName = "Sign in with Google",
                IsEnabled = true
            });
        }

        if (_settings.Apple.IsConfigured)
        {
            providers.Add(new ExternalAuthProviderDto
            {
                Provider = "Apple",
                DisplayName = "Sign in with Apple",
                IsEnabled = true
            });
        }

        if (_settings.OpenIdConnect.IsConfigured)
        {
            providers.Add(new ExternalAuthProviderDto
            {
                Provider = "OIDC",
                DisplayName = _settings.OpenIdConnect.DisplayName,
                IsEnabled = true
            });
        }

        return Task.FromResult(providers);
    }

    /// <inheritdoc />
    public async Task<ExternalAuthChallengeResponse> GetAuthorizationUrlAsync(
        string provider,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        // Generate cryptographically secure state
        var state = GenerateState();

        // Generate PKCE code verifier and challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Store state and code verifier in cache
        var cacheKey = $"oauth_state_{state}";
        _cache.Set(cacheKey, new OAuthStateData
        {
            Provider = provider,
            RedirectUri = redirectUri,
            CodeVerifier = codeVerifier
        }, TimeSpan.FromMinutes(StateExpirationMinutes));

        var authUrl = provider.ToUpperInvariant() switch
        {
            "GOOGLE" => BuildGoogleAuthUrl(redirectUri, state, codeChallenge),
            "APPLE" => BuildAppleAuthUrl(redirectUri, state, codeChallenge),
            "OIDC" => await BuildOidcAuthUrlAsync(redirectUri, state, codeChallenge, cancellationToken),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        return new ExternalAuthChallengeResponse
        {
            AuthorizationUrl = authUrl,
            State = state
        };
    }

    /// <inheritdoc />
    public async Task<ExternalAuthChallengeResponse> GetLinkAuthorizationUrlAsync(
        Guid userId,
        string provider,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        // Generate cryptographically secure state
        var state = GenerateState();

        // Generate PKCE code verifier and challenge
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Store state, code verifier, and user ID in cache for link operation
        var cacheKey = $"oauth_state_{state}";
        _cache.Set(cacheKey, new OAuthStateData
        {
            Provider = provider,
            RedirectUri = redirectUri,
            CodeVerifier = codeVerifier,
            LinkUserId = userId,
            IsLinkOperation = true
        }, TimeSpan.FromMinutes(StateExpirationMinutes));

        var authUrl = provider.ToUpperInvariant() switch
        {
            "GOOGLE" => BuildGoogleAuthUrl(redirectUri, state, codeChallenge),
            "APPLE" => BuildAppleAuthUrl(redirectUri, state, codeChallenge),
            "OIDC" => await BuildOidcAuthUrlAsync(redirectUri, state, codeChallenge, cancellationToken),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        return new ExternalAuthChallengeResponse
        {
            AuthorizationUrl = authUrl,
            State = state
        };
    }

    /// <inheritdoc />
    public async Task<LoginResponse> ProcessCallbackAsync(
        string provider,
        ExternalAuthCallbackRequest request,
        string redirectUri,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        // Verify state
        var cacheKey = $"oauth_state_{request.State}";
        if (!_cache.TryGetValue<OAuthStateData>(cacheKey, out var stateData))
        {
            _logger.LogWarning("Invalid or expired OAuth state");
            throw new InvalidCredentialsException("Invalid or expired OAuth state");
        }

        // Remove state from cache (one-time use)
        _cache.Remove(cacheKey);

        // Validate provider matches
        if (!stateData!.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("OAuth provider mismatch: expected {Expected}, got {Got}", stateData.Provider, provider);
            throw new InvalidCredentialsException("OAuth provider mismatch");
        }

        // Use the redirect URI stored during challenge creation (must match exactly)
        var storedRedirectUri = stateData.RedirectUri;

        // Exchange code for tokens and get user info
        var userInfo = provider.ToUpperInvariant() switch
        {
            "GOOGLE" => await ExchangeGoogleCodeAsync(request.Code, storedRedirectUri, stateData.CodeVerifier, cancellationToken),
            "APPLE" => await ExchangeAppleCodeAsync(request.Code, storedRedirectUri, stateData.CodeVerifier, cancellationToken),
            "OIDC" => await ExchangeOidcCodeAsync(request.Code, storedRedirectUri, stateData.CodeVerifier, cancellationToken),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        // Find or create user
        var user = await FindOrCreateUserAsync(provider, userInfo, cancellationToken);

        // Generate tokens
        return await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LinkedAccountDto> LinkProviderAsync(
        Guid userId,
        string provider,
        ExternalAuthLinkRequest request,
        string redirectUri,
        CancellationToken cancellationToken = default)
    {
        // Verify state
        var cacheKey = $"oauth_state_{request.State}";
        if (!_cache.TryGetValue<OAuthStateData>(cacheKey, out var stateData))
        {
            throw new InvalidCredentialsException("Invalid or expired OAuth state");
        }

        _cache.Remove(cacheKey);

        // Use the redirect URI stored during challenge creation (must match exactly)
        var storedRedirectUri = stateData!.RedirectUri;

        // Get user info from provider
        var userInfo = provider.ToUpperInvariant() switch
        {
            "GOOGLE" => await ExchangeGoogleCodeAsync(request.Code, storedRedirectUri, stateData.CodeVerifier, cancellationToken),
            "APPLE" => await ExchangeAppleCodeAsync(request.Code, storedRedirectUri, stateData.CodeVerifier, cancellationToken),
            "OIDC" => await ExchangeOidcCodeAsync(request.Code, storedRedirectUri, stateData.CodeVerifier, cancellationToken),
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };

        // Check if this provider account is already linked to another user
        var existingLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(uel =>
                uel.Provider == provider &&
                uel.ProviderUserId == userInfo.ProviderId &&
                uel.UserId != userId,
                cancellationToken);

        if (existingLink != null)
        {
            throw new DuplicateEntityException("ExternalLogin", "Provider", $"{provider} account is already linked to another user");
        }

        // Check if user already has this provider linked
        var userLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(uel => uel.UserId == userId && uel.Provider == provider, cancellationToken);

        if (userLink != null)
        {
            throw new DuplicateEntityException("ExternalLogin", "Provider", $"{provider} is already linked to your account");
        }

        // Get user
        var user = await _context.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        // Create link
        var externalLogin = new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = userId,
            Provider = provider,
            ProviderUserId = userInfo.ProviderId,
            ProviderDisplayName = userInfo.Name,
            ProviderEmail = userInfo.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserExternalLogins.Add(externalLogin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Linked {Provider} account to user {UserId}", provider, userId);

        return new LinkedAccountDto
        {
            Id = externalLogin.Id,
            Provider = provider,
            ProviderDisplayName = GetProviderDisplayName(provider),
            ProviderEmail = userInfo.Email,
            LinkedAt = externalLogin.CreatedAt,
            LastUsedAt = null
        };
    }

    /// <inheritdoc />
    public async Task UnlinkProviderAsync(
        Guid userId,
        string provider,
        CancellationToken cancellationToken = default)
    {
        var externalLogin = await _context.UserExternalLogins
            .FirstOrDefaultAsync(uel => uel.UserId == userId && uel.Provider == provider, cancellationToken);

        if (externalLogin == null)
        {
            throw new InvalidOperationException($"{provider} is not linked to user {userId}");
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
        var otherExternalLogins = user.ExternalLogins.Count(el => el.Provider != provider);
        var hasPasskeys = user.PasskeyCredentials.Count > 0;

        if (!hasPassword && otherExternalLogins == 0 && !hasPasskeys)
        {
            throw new InvalidOperationException("Cannot unlink the last authentication method. Please add a password or another login method first.");
        }

        _context.UserExternalLogins.Remove(externalLogin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Unlinked {Provider} account from user {UserId}", provider, userId);
    }

    /// <inheritdoc />
    public async Task<List<LinkedAccountDto>> GetLinkedAccountsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var externalLogins = await _context.UserExternalLogins
            .Where(uel => uel.UserId == userId)
            .OrderBy(uel => uel.Provider)
            .ToListAsync(cancellationToken);

        return externalLogins.Select(el => new LinkedAccountDto
        {
            Id = el.Id,
            Provider = el.Provider,
            ProviderDisplayName = GetProviderDisplayName(el.Provider),
            ProviderEmail = el.ProviderEmail,
            LinkedAt = el.CreatedAt,
            LastUsedAt = el.LastUsedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<LoginResponse> ProcessNativeAppleSignInAsync(
        NativeAppleSignInRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            throw new InvalidCredentialsException("Identity token is required");
        }

        var userInfo = await ValidateAppleIdentityTokenAsync(request, cancellationToken);
        var user = await FindOrCreateUserAsync("Apple", userInfo, cancellationToken);
        return await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LoginResponse> ProcessNativeGoogleSignInAsync(
        NativeGoogleSignInRequest request,
        string ipAddress,
        string deviceInfo,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw new InvalidCredentialsException("ID token is required");
        }

        var userInfo = await ValidateGoogleIdTokenAsync(request.IdToken, cancellationToken);
        var user = await FindOrCreateUserAsync("Google", userInfo, cancellationToken);
        return await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    private async Task<ExternalUserInfo> ValidateAppleIdentityTokenAsync(
        NativeAppleSignInRequest request,
        CancellationToken cancellationToken)
    {
        if (!_settings.Apple.Enabled)
        {
            throw new InvalidCredentialsException("Sign in with Apple is not enabled");
        }

        var jwt = await ValidateProviderIdTokenAsync(
            "Apple",
            request.IdentityToken,
            AppleJwksUri,
            [AppleIssuer],
            [_settings.Apple.ClientId, _settings.Apple.BundleId],
            cancellationToken);

        var providerId = jwt.Subject;

        if (!string.IsNullOrEmpty(request.UserIdentifier) && request.UserIdentifier != providerId)
        {
            _logger.LogWarning("Apple user identifier does not match the token subject");
            throw new InvalidCredentialsException("User identifier mismatch");
        }

        // Apple only sends the name to the device, and only on first sign-in, so it has to come
        // from the request. It is display data. The email is not: it decides which account the
        // token signs in to, so it comes from the signed token or not at all.
        var givenName = request.FullName?.GivenName;
        var familyName = request.FullName?.FamilyName;

        return new ExternalUserInfo
        {
            ProviderId = providerId,
            Email = VerifiedEmail("Apple", jwt),
            Name = string.IsNullOrEmpty(givenName) ? null : $"{givenName} {familyName}".Trim(),
            GivenName = givenName,
            FamilyName = familyName
        };
    }

    private async Task<ExternalUserInfo> ValidateGoogleIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken)
    {
        if (!_settings.Google.Enabled)
        {
            throw new InvalidCredentialsException("Google sign-in is not enabled");
        }

        var jwt = await ValidateProviderIdTokenAsync(
            "Google",
            idToken,
            GoogleJwksUri,
            GoogleIssuers,
            [_settings.Google.ClientId, _settings.Google.IosClientId, _settings.Google.AndroidClientId],
            cancellationToken);

        return new ExternalUserInfo
        {
            ProviderId = jwt.Subject,
            Email = VerifiedEmail("Google", jwt),
            Name = ClaimValue(jwt, "name"),
            GivenName = ClaimValue(jwt, "given_name"),
            FamilyName = ClaimValue(jwt, "family_name")
        };
    }

    // ---------------------------------------------------------------------
    // ID token verification for the native endpoints.
    //
    // These tokens arrive in a request body from the device, so nothing about their transport
    // vouches for them. Issuer, audience and expiry are all public or forgeable; only the
    // provider's RS256 signature proves the token came from Google or Apple. The browser
    // code-exchange paths are different: they receive the id_token from the provider's token
    // endpoint over TLS, which is why they still only parse it.
    // ---------------------------------------------------------------------

    public const string GoogleJwksUri = "https://www.googleapis.com/oauth2/v3/certs";
    public const string AppleJwksUri = "https://appleid.apple.com/auth/keys";
    private const string AppleIssuer = "https://appleid.apple.com";
    private static readonly string[] GoogleIssuers = ["https://accounts.google.com", "accounts.google.com"];

    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(6);

    // An unknown kid triggers one refetch, so a key rotation is picked up without a restart.
    // The floor stops a stream of tokens with made-up kids from turning into a request to the
    // provider apiece.
    private static readonly TimeSpan JwksMinRefreshInterval = TimeSpan.FromMinutes(5);

    private async Task<JsonWebToken> ValidateProviderIdTokenAsync(
        string provider,
        string token,
        string jwksUri,
        IReadOnlyCollection<string> validIssuers,
        IEnumerable<string?> audiences,
        CancellationToken cancellationToken)
    {
        var validAudiences = audiences.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!).ToList();
        if (validAudiences.Count == 0)
        {
            _logger.LogError("{Provider} sign-in is enabled but no client id is configured", provider);
            throw new InvalidCredentialsException($"{provider} sign-in is not configured");
        }

        var handler = new JsonWebTokenHandler();
        var keys = await GetProviderSigningKeysAsync(jwksUri, forceRefresh: false, cancellationToken);
        var result = await handler.ValidateTokenAsync(token, BuildValidationParameters(validIssuers, validAudiences, keys));

        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            keys = await GetProviderSigningKeysAsync(jwksUri, forceRefresh: true, cancellationToken);
            result = await handler.ValidateTokenAsync(token, BuildValidationParameters(validIssuers, validAudiences, keys));
        }

        if (!result.IsValid)
        {
            _logger.LogWarning("Rejected {Provider} ID token: {Reason}",
                provider, result.Exception?.GetType().Name ?? "invalid");
            throw new InvalidCredentialsException("Invalid identity token");
        }

        var jwt = (JsonWebToken)result.SecurityToken;
        if (string.IsNullOrEmpty(jwt.Subject))
        {
            throw new InvalidCredentialsException("Invalid identity token");
        }

        _logger.LogInformation("Validated {Provider} ID token for subject {ProviderId}", provider, jwt.Subject);
        return jwt;
    }

    private static TokenValidationParameters BuildValidationParameters(
        IReadOnlyCollection<string> validIssuers,
        IReadOnlyCollection<string> validAudiences,
        IReadOnlyCollection<SecurityKey> keys) => new()
    {
        ValidateIssuer = true,
        ValidIssuers = validIssuers,
        ValidateAudience = true,
        ValidAudiences = validAudiences,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromMinutes(2),
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        IssuerSigningKeys = keys,
        // Pinned: both providers sign with RS256, and accepting anything else is how alg
        // confusion (HS256 keyed with a public key, or "none") gets in.
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        TryAllIssuerSigningKeys = false,
    };

    private async Task<IReadOnlyCollection<SecurityKey>> GetProviderSigningKeysAsync(
        string jwksUri,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"provider_jwks_{jwksUri}";
        var forcedAtKey = $"provider_jwks_forced_{jwksUri}";

        if (_cache.TryGetValue<IReadOnlyCollection<SecurityKey>>(cacheKey, out var cached) && cached is not null)
        {
            if (!forceRefresh || _cache.TryGetValue<DateTime>(forcedAtKey, out _))
            {
                return cached;
            }

            _cache.Set(forcedAtKey, DateTime.UtcNow, JwksMinRefreshInterval);
        }

        var client = _httpClientFactory.CreateClient();
        using var response = await client.GetAsync(jwksUri, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Fetching signing keys from {JwksUri} failed with {Status}", jwksUri, (int)response.StatusCode);
            if (cached is not null) return cached;
            throw new InvalidOperationException($"Could not fetch signing keys from {jwksUri}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        IReadOnlyCollection<SecurityKey> keys = new JsonWebKeySet(json).GetSigningKeys().ToList();

        _cache.Set(cacheKey, keys, JwksCacheDuration);
        return keys;
    }

    /// <summary>
    /// The token's email, but only when the provider says it is verified. An unverified email
    /// is treated as absent, so the token can still sign in through an existing
    /// (provider, subject) link but cannot claim an account by address.
    /// </summary>
    private string? VerifiedEmail(string provider, JsonWebToken jwt)
    {
        var email = ClaimValue(jwt, "email");
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        // Google sends a JSON boolean; Apple has sent both a boolean and the string "true".
        if (string.Equals(ClaimValue(jwt, "email_verified"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return email;
        }

        _logger.LogWarning("{Provider} ID token carries an unverified email; ignoring it", provider);
        return null;
    }

    private static string? ClaimValue(JsonWebToken jwt, string type) =>
        jwt.TryGetClaim(type, out var claim) ? claim.Value : null;

    /// <inheritdoc />
    public async Task<LinkedAccountDto> LinkNativeAppleAsync(
        Guid userId,
        NativeAppleSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdentityToken))
        {
            throw new InvalidCredentialsException("Identity token is required");
        }

        var userInfo = await ValidateAppleIdentityTokenAsync(request, cancellationToken);
        return await LinkNativeProviderAsync(userId, "Apple", userInfo, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<LinkedAccountDto> LinkNativeGoogleAsync(
        Guid userId,
        NativeGoogleSignInRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            throw new InvalidCredentialsException("ID token is required");
        }

        var userInfo = await ValidateGoogleIdTokenAsync(request.IdToken, cancellationToken);
        return await LinkNativeProviderAsync(userId, "Google", userInfo, cancellationToken);
    }

    private async Task<LinkedAccountDto> LinkNativeProviderAsync(
        Guid userId,
        string provider,
        ExternalUserInfo userInfo,
        CancellationToken cancellationToken)
    {
        // Check if this provider account is already linked to another user
        var existingLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(uel =>
                uel.Provider == provider &&
                uel.ProviderUserId == userInfo.ProviderId &&
                uel.UserId != userId,
                cancellationToken);

        if (existingLink != null)
        {
            throw new DuplicateEntityException("ExternalLogin", "Provider", $"{provider} account is already linked to another user");
        }

        // Check if user already has this provider linked
        var userLink = await _context.UserExternalLogins
            .FirstOrDefaultAsync(uel => uel.UserId == userId && uel.Provider == provider, cancellationToken);

        if (userLink != null)
        {
            throw new DuplicateEntityException("ExternalLogin", "Provider", $"{provider} is already linked to your account");
        }

        // Get user
        var user = await _context.Users.FindAsync([userId], cancellationToken);
        if (user == null)
        {
            throw new EntityNotFoundException("User", userId);
        }

        // Create link
        var externalLogin = new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            TenantId = user.TenantId,
            UserId = userId,
            Provider = provider,
            ProviderUserId = userInfo.ProviderId,
            ProviderDisplayName = userInfo.Name,
            ProviderEmail = userInfo.Email,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserExternalLogins.Add(externalLogin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Linked {Provider} account (native) to user {UserId}", provider, userId);

        return new LinkedAccountDto
        {
            Id = externalLogin.Id,
            Provider = provider,
            ProviderDisplayName = GetProviderDisplayName(provider),
            ProviderEmail = userInfo.Email,
            LinkedAt = externalLogin.CreatedAt,
            LastUsedAt = null
        };
    }

    #region Helper Methods

    private string BuildGoogleAuthUrl(string redirectUri, string state, string codeChallenge)
    {
        var scopes = "openid email profile";
        var authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={Uri.EscapeDataString(_settings.Google.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&response_type=code"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
            + $"&code_challenge_method=S256"
            + $"&access_type=offline"
            + $"&prompt=select_account";

        return authUrl;
    }

    private string BuildAppleAuthUrl(string redirectUri, string state, string codeChallenge)
    {
        var scopes = "name email";
        // PKCE (Apple supports code_challenge_method=S256 on /auth/authorize
        // since 2020). Matches Google + OIDC. The state cache already stores
        // the code_verifier; ExchangeAppleCodeAsync sends it back at the
        // /auth/token exchange to satisfy the challenge.
        var authUrl = "https://appleid.apple.com/auth/authorize"
            + $"?client_id={Uri.EscapeDataString(_settings.Apple.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&response_type=code"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
            + $"&code_challenge_method=S256"
            + $"&response_mode=form_post";

        return authUrl;
    }

    private async Task<string> BuildOidcAuthUrlAsync(string redirectUri, string state, string codeChallenge, CancellationToken cancellationToken)
    {
        // Fetch the OIDC discovery document to get the authorization endpoint
        var discovery = await GetOidcDiscoveryDocumentAsync(cancellationToken);

        var scopes = "openid profile email";
        if (_settings.OpenIdConnect.Scopes.Length > 0)
        {
            scopes = string.Join(" ", _settings.OpenIdConnect.Scopes);
        }

        var authUrl = discovery.AuthorizationEndpoint
            + $"?client_id={Uri.EscapeDataString(_settings.OpenIdConnect.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&response_type=code"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}"
            + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
            + $"&code_challenge_method=S256";

        return authUrl;
    }

    private async Task<ExternalUserInfo> ExchangeGoogleCodeAsync(
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        // Exchange code for tokens
        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = _settings.Google.ClientId,
            ["client_secret"] = _settings.Google.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        };

        var tokenResponse = await client.PostAsync(
            "https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(tokenRequest),
            cancellationToken);

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Google token exchange failed: {Response}", tokenContent);
            throw new InvalidCredentialsException("Failed to exchange authorization code");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        var idToken = tokenData.GetProperty("id_token").GetString()!;

        // Parse ID token to get user info
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);

        return new ExternalUserInfo
        {
            ProviderId = jwt.Claims.First(c => c.Type == "sub").Value,
            Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
            Name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value,
            GivenName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value,
            FamilyName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value
        };
    }

    private async Task<ExternalUserInfo> ExchangeAppleCodeAsync(
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        // Generate client secret (JWT signed with Apple private key)
        var clientSecret = GenerateAppleClientSecret();

        // PKCE — code_verifier must match the SHA-256 challenge sent at
        // /auth/authorize. Required now that BuildAppleAuthUrl always
        // includes code_challenge.
        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = _settings.Apple.ClientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier,
            ["grant_type"] = "authorization_code"
        };

        var tokenResponse = await client.PostAsync(
            "https://appleid.apple.com/auth/token",
            new FormUrlEncodedContent(tokenRequest),
            cancellationToken);

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Apple token exchange failed: {Response}", tokenContent);
            throw new InvalidCredentialsException("Failed to exchange authorization code");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        var idToken = tokenData.GetProperty("id_token").GetString()!;

        // Parse ID token
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);

        return new ExternalUserInfo
        {
            ProviderId = jwt.Claims.First(c => c.Type == "sub").Value,
            Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
            // Apple provides name only on first authentication
            Name = null,
            GivenName = null,
            FamilyName = null
        };
    }

    private async Task<ExternalUserInfo> ExchangeOidcCodeAsync(
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        // Fetch the OIDC discovery document to get the token endpoint
        var discovery = await GetOidcDiscoveryDocumentAsync(cancellationToken);

        var client = _httpClientFactory.CreateClient();

        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = _settings.OpenIdConnect.ClientId,
            ["client_secret"] = _settings.OpenIdConnect.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["code_verifier"] = codeVerifier
        };

        var tokenResponse = await client.PostAsync(
            discovery.TokenEndpoint,
            new FormUrlEncodedContent(tokenRequest),
            cancellationToken);

        var tokenContent = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            _logger.LogError("OIDC token exchange failed: {Response}", tokenContent);
            throw new InvalidCredentialsException("Failed to exchange authorization code");
        }

        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);
        var idToken = tokenData.GetProperty("id_token").GetString()!;

        // Parse ID token
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);

        return new ExternalUserInfo
        {
            ProviderId = jwt.Claims.First(c => c.Type == "sub").Value,
            Email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value,
            Name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value,
            GivenName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value,
            FamilyName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value
        };
    }

    private string GenerateAppleClientSecret()
    {
        // Load the private key
        var privateKey = _settings.Apple.PrivateKey;

        // Create the key
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKey);

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = _settings.Apple.KeyId };
        var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var now = DateTime.UtcNow;
        var handler = new JwtSecurityTokenHandler();

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Issuer = _settings.Apple.TeamId,
            Audience = "https://appleid.apple.com",
            Subject = new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim("sub", _settings.Apple.ClientId)
            }),
            NotBefore = now,
            Expires = now.AddMinutes(5),
            SigningCredentials = signingCredentials
        };

        var token = handler.CreateToken(tokenDescriptor);
        return handler.WriteToken(token);
    }

    private async Task<User> FindOrCreateUserAsync(
        string provider,
        ExternalUserInfo userInfo,
        CancellationToken cancellationToken)
    {
        // An existing (provider, subject) link is the account, whatever the token's email says
        // today. Look it up first, so an email change at the provider or a token without one
        // never falls through to the email match.
        var existingExternalLogin = await _context.UserExternalLogins
            .Include(uel => uel.User)
            .FirstOrDefaultAsync(uel =>
                uel.Provider == provider &&
                uel.ProviderUserId == userInfo.ProviderId,
                cancellationToken);

        if (existingExternalLogin != null)
        {
            // Update last used
            existingExternalLogin.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("User {UserId} logged in via {Provider}", existingExternalLogin.UserId, provider);
            return existingExternalLogin.User;
        }

        if (string.IsNullOrEmpty(userInfo.Email))
        {
            throw new InvalidCredentialsException("Email is required for authentication");
        }

        var email = userInfo.Email.ToLower().Trim();

        // Check if user exists with this email
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (existingUser != null)
        {
            // Link external login to an existing user. The tenant comes from the user, not from
            // configuration: SelfHosted:TenantId is unset on a multi-tenant host, so reading it
            // here stamped every link row with the single-tenant fallback id rather than the
            // user's own household. Nothing complained, because this callback runs on a request
            // whose tenant is usually unresolved and the global filter then matches every row —
            // right up until something does resolve a tenant, at which point the row vanishes
            // behind the filter. Same silent failure a mismatched passkey credential had before
            // the PasskeyService fix.
            var externalLogin = new UserExternalLogin
            {
                Id = Guid.NewGuid(),
                TenantId = existingUser.TenantId,
                UserId = existingUser.Id,
                Provider = provider,
                ProviderUserId = userInfo.ProviderId,
                ProviderDisplayName = userInfo.Name,
                ProviderEmail = userInfo.Email,
                LastUsedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.UserExternalLogins.Add(externalLogin);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Linked {Provider} to existing user {UserId}", provider, existingUser.Id);
            return existingUser;
        }

        // Check if this is the first user (initial setup) - only then allow auto-creation
        var isFirstUser = !await _context.Users.AnyAsync(cancellationToken);

        if (!isFirstUser)
        {
            // Not initial setup - user must already exist
            _logger.LogWarning("OAuth login attempted for non-existent user: {Email} via {Provider}", email, provider);
            throw new InvalidCredentialsException("No account exists with this email address. Please contact your administrator to create an account.");
        }

        // Initial setup - create the first user as Admin
        _logger.LogInformation("Creating first user via OAuth: {Email} via {Provider}", email, provider);

        // First run only, per the isFirstUser check above, which is what makes configuration a
        // sound source for the tenant here. No silent fallback though: a host that failed to set
        // the key would write the household into a tenant nothing else resolves to, and the
        // account would sign in to an app that is empty forever rather than fail. Program.cs
        // asserts in Production that this agrees with FixedTenantId, and every shipped
        // self-hosted deployment asset sets it explicitly — including the Home Assistant add-on,
        // which generates a random per-install UUID and so would be actively misfiled by the
        // old fallback.
        if (!Guid.TryParse(_configuration["SelfHosted:TenantId"], out var tenantId))
        {
            throw new InvalidOperationException(
                "SelfHosted:TenantId is not configured, so a new user cannot be assigned to " +
                "a household. Set it to the same value as FixedTenantId.");
        }

        var firstName = userInfo.GivenName ?? userInfo.Name?.Split(' ').FirstOrDefault() ?? "User";
        var lastName = userInfo.FamilyName ?? userInfo.Name?.Split(' ').LastOrDefault() ?? "";

        var user = new User
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Email = email,
            Username = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = string.Empty, // No password for external auth users
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        var userRole = new UserRole
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Role = Role.Admin, // First user is always Admin
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserRoles.Add(userRole);

        // Create external login record
        var newExternalLogin = new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Provider = provider,
            ProviderUserId = userInfo.ProviderId,
            ProviderDisplayName = userInfo.Name,
            ProviderEmail = userInfo.Email,
            LastUsedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.UserExternalLogins.Add(newExternalLogin);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created first user {UserId} via {Provider} as Admin", user.Id, provider);

        // Create contact for the user
        try
        {
            await _contactService.CreateContactForUserAsync(user, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create contact for user {UserId}", user.Id);
        }

        return user;
    }

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

        // Check user is active
        if (!loadedUser.IsActive)
        {
            throw new AccountInactiveException();
        }

        // Get permissions and roles
        var permissions = loadedUser.UserPermissions.Select(up => up.Permission.Name).ToList();
        var roles = loadedUser.UserRoles.Select(ur => ur.Role).ToList();

        // Generate tokens
        // First-factor authentication completed now; the refresh token's family
        // records the same auth_time the access token carries.
        var authTime = DateTime.UtcNow;
        var accessToken = _tokenService.GenerateAccessToken(
            loadedUser, permissions, roles, authTime: authTime);
        var accessTokenExpiration = _tokenService.GetTokenExpiration();
        var refreshTokenString = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = HashToken(refreshTokenString);

        var defaultExpirationDays = _configuration.GetValue("JwtSettings:RefreshTokenExpirationDays", 7);
        var extendedExpirationDays = _configuration.GetValue("JwtSettings:RefreshTokenExtendedExpirationDays", 30);
        var refreshTokenExpirationDays = rememberMe ? extendedExpirationDays : defaultExpirationDays;

        var refreshToken = RefreshToken.StartFamily(
            loadedUser.Id, loadedUser.TenantId, refreshTokenHash,
            expiresAt: DateTime.UtcNow.AddDays(refreshTokenExpirationDays),
            authTime,
            deviceInfo, ipAddress, rememberMe);

        _context.RefreshTokens.Add(refreshToken);

        loadedUser.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var userDto = AuthenticationMapper.ToDto(loadedUser);

        // Phase 4 chunk 4.D — same local-server hydration as password login.
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

    private static string GenerateState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier));
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private string GetProviderDisplayName(string provider)
    {
        return provider.ToUpperInvariant() switch
        {
            "GOOGLE" => "Google",
            "APPLE" => "Apple",
            "OIDC" => _settings.OpenIdConnect.DisplayName,
            _ => provider
        };
    }

    #endregion

    #region Helper Classes

    private class OAuthStateData
    {
        public string Provider { get; set; } = string.Empty;
        public string RedirectUri { get; set; } = string.Empty;
        public string CodeVerifier { get; set; } = string.Empty;
        public Guid? LinkUserId { get; set; }
        public bool IsLinkOperation { get; set; }
    }

    private class ExternalUserInfo
    {
        public string ProviderId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Name { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
    }

    private class OidcDiscoveryDocument
    {
        public string AuthorizationEndpoint { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string? UserinfoEndpoint { get; set; }
        public string? JwksUri { get; set; }
        public string? Issuer { get; set; }
    }

    #endregion

    #region OIDC Discovery

    private async Task<OidcDiscoveryDocument> GetOidcDiscoveryDocumentAsync(CancellationToken cancellationToken = default)
    {
        var cacheKey = $"oidc_discovery_{_settings.OpenIdConnect.Authority}";

        if (_cache.TryGetValue<OidcDiscoveryDocument>(cacheKey, out var cached) && cached != null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient();
        var discoveryUrl = $"{_settings.OpenIdConnect.Authority.TrimEnd('/')}/.well-known/openid-configuration";

        _logger.LogDebug("Fetching OIDC discovery document from {Url}", discoveryUrl);

        var response = await client.GetAsync(discoveryUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to fetch OIDC discovery document from {Url}: {StatusCode} - {Content}",
                discoveryUrl, response.StatusCode, content);
            throw new InvalidOperationException($"Failed to fetch OIDC discovery document: {response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonSerializer.Deserialize<JsonElement>(json);

        var discovery = new OidcDiscoveryDocument
        {
            AuthorizationEndpoint = doc.GetProperty("authorization_endpoint").GetString() ?? throw new InvalidOperationException("Missing authorization_endpoint"),
            TokenEndpoint = doc.GetProperty("token_endpoint").GetString() ?? throw new InvalidOperationException("Missing token_endpoint"),
            UserinfoEndpoint = doc.TryGetProperty("userinfo_endpoint", out var userinfo) ? userinfo.GetString() : null,
            JwksUri = doc.TryGetProperty("jwks_uri", out var jwks) ? jwks.GetString() : null,
            Issuer = doc.TryGetProperty("issuer", out var issuer) ? issuer.GetString() : null
        };

        // Cache for 1 hour
        _cache.Set(cacheKey, discovery, TimeSpan.FromHours(1));

        _logger.LogDebug("OIDC discovery: authorization_endpoint={AuthEndpoint}, token_endpoint={TokenEndpoint}",
            discovery.AuthorizationEndpoint, discovery.TokenEndpoint);

        return discovery;
    }

    #endregion
}
