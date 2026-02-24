using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Service for handling external authentication providers (Google, Apple, OIDC)
/// </summary>
public class ExternalAuthService : IExternalAuthService
{
    private readonly HomeManagementDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IContactService _contactService;
    private readonly IMemoryCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ExternalAuthSettings _settings;
    private readonly ILogger<ExternalAuthService> _logger;

    private const int StateExpirationMinutes = 10;

    public ExternalAuthService(
        HomeManagementDbContext context,
        ITokenService tokenService,
        IMapper mapper,
        IConfiguration configuration,
        IContactService contactService,
        IMemoryCache cache,
        IHttpClientFactory httpClientFactory,
        IOptions<ExternalAuthSettings> settings,
        ILogger<ExternalAuthService> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _contactService = contactService;
        _cache = cache;
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
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
            "APPLE" => BuildAppleAuthUrl(redirectUri, state),
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
            "APPLE" => BuildAppleAuthUrl(redirectUri, state),
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
            "APPLE" => await ExchangeAppleCodeAsync(request.Code, storedRedirectUri, cancellationToken),
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
            "APPLE" => await ExchangeAppleCodeAsync(request.Code, storedRedirectUri, cancellationToken),
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

        // Validate and parse the Apple identity token
        var userInfo = await ValidateAppleIdentityTokenAsync(request, cancellationToken);

        // Find or create user
        var user = await FindOrCreateUserAsync("Apple", userInfo, cancellationToken);

        // Generate tokens
        return await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    private async Task<ExternalUserInfo> ValidateAppleIdentityTokenAsync(
        NativeAppleSignInRequest request,
        CancellationToken cancellationToken)
    {
        // Parse the identity token (JWT)
        var handler = new JwtSecurityTokenHandler();

        if (!handler.CanReadToken(request.IdentityToken))
        {
            _logger.LogWarning("Invalid Apple identity token format");
            throw new InvalidCredentialsException("Invalid identity token format");
        }

        var jwt = handler.ReadJwtToken(request.IdentityToken);

        // Validate token claims
        var issuer = jwt.Issuer;
        if (issuer != "https://appleid.apple.com")
        {
            _logger.LogWarning("Invalid Apple identity token issuer: {Issuer}", issuer);
            throw new InvalidCredentialsException("Invalid identity token issuer");
        }

        // Check audience matches our client ID
        var audience = jwt.Audiences.FirstOrDefault();
        if (audience != _settings.Apple.ClientId && audience != _settings.Apple.BundleId)
        {
            _logger.LogWarning("Invalid Apple identity token audience: {Audience}, expected {ClientId} or {BundleId}",
                audience, _settings.Apple.ClientId, _settings.Apple.BundleId);
            throw new InvalidCredentialsException("Invalid identity token audience");
        }

        // Check token is not expired
        if (jwt.ValidTo < DateTime.UtcNow)
        {
            _logger.LogWarning("Apple identity token has expired");
            throw new InvalidCredentialsException("Identity token has expired");
        }

        // For production, you should also validate the signature using Apple's public keys
        // fetched from https://appleid.apple.com/auth/keys
        // For now, we trust the token from the native SDK

        // Extract user info from the token
        var providerId = jwt.Claims.First(c => c.Type == "sub").Value;
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

        // Apple only provides name/email on first sign-in, use the request data if available
        var givenName = request.FullName?.GivenName;
        var familyName = request.FullName?.FamilyName;

        // If email is not in token, use the one from the request (only available on first sign-in)
        if (string.IsNullOrEmpty(email))
        {
            email = request.Email;
        }

        // Validate user identifier matches if provided
        if (!string.IsNullOrEmpty(request.UserIdentifier) && request.UserIdentifier != providerId)
        {
            _logger.LogWarning("User identifier mismatch: token={TokenId}, request={RequestId}",
                providerId, request.UserIdentifier);
            throw new InvalidCredentialsException("User identifier mismatch");
        }

        _logger.LogInformation("Validated Apple identity token for user {ProviderId}", providerId);

        return new ExternalUserInfo
        {
            ProviderId = providerId,
            Email = email,
            Name = string.IsNullOrEmpty(givenName) ? null : $"{givenName} {familyName}".Trim(),
            GivenName = givenName,
            FamilyName = familyName
        };
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

        // Validate and parse the Google ID token
        var userInfo = ValidateGoogleIdToken(request.IdToken);

        // Find or create user
        var user = await FindOrCreateUserAsync("Google", userInfo, cancellationToken);

        // Generate tokens
        return await GenerateLoginResponseAsync(user, ipAddress, deviceInfo, request.RememberMe, cancellationToken);
    }

    private ExternalUserInfo ValidateGoogleIdToken(string idToken)
    {
        // Parse the ID token (JWT)
        var handler = new JwtSecurityTokenHandler();

        if (!handler.CanReadToken(idToken))
        {
            _logger.LogWarning("Invalid Google ID token format");
            throw new InvalidCredentialsException("Invalid ID token format");
        }

        var jwt = handler.ReadJwtToken(idToken);

        // Validate token claims
        var issuer = jwt.Issuer;
        if (issuer != "https://accounts.google.com" && issuer != "accounts.google.com")
        {
            _logger.LogWarning("Invalid Google ID token issuer: {Issuer}", issuer);
            throw new InvalidCredentialsException("Invalid ID token issuer");
        }

        // Check audience matches one of our client IDs (web, iOS, or Android)
        var audience = jwt.Audiences.FirstOrDefault();
        var validAudiences = new[]
        {
            _settings.Google.ClientId,
            _settings.Google.IosClientId,
            _settings.Google.AndroidClientId
        }.Where(a => !string.IsNullOrEmpty(a)).ToList();

        if (!validAudiences.Contains(audience))
        {
            _logger.LogWarning("Invalid Google ID token audience: {Audience}, expected one of: {ValidAudiences}",
                audience, string.Join(", ", validAudiences));
            throw new InvalidCredentialsException("Invalid ID token audience");
        }

        // Check token is not expired
        if (jwt.ValidTo < DateTime.UtcNow)
        {
            _logger.LogWarning("Google ID token has expired");
            throw new InvalidCredentialsException("ID token has expired");
        }

        // For production, you should also validate the signature using Google's public keys
        // fetched from https://www.googleapis.com/oauth2/v3/certs
        // For now, we trust the token from the native SDK

        // Extract user info from the token
        var providerId = jwt.Claims.First(c => c.Type == "sub").Value;
        var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
        var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
        var givenName = jwt.Claims.FirstOrDefault(c => c.Type == "given_name")?.Value;
        var familyName = jwt.Claims.FirstOrDefault(c => c.Type == "family_name")?.Value;

        _logger.LogInformation("Validated Google ID token for user {ProviderId}", providerId);

        return new ExternalUserInfo
        {
            ProviderId = providerId,
            Email = email,
            Name = name,
            GivenName = givenName,
            FamilyName = familyName
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

    private string BuildAppleAuthUrl(string redirectUri, string state)
    {
        var scopes = "name email";
        var authUrl = "https://appleid.apple.com/auth/authorize"
            + $"?client_id={Uri.EscapeDataString(_settings.Apple.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + $"&response_type=code"
            + $"&scope={Uri.EscapeDataString(scopes)}"
            + $"&state={Uri.EscapeDataString(state)}"
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
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();

        // Generate client secret (JWT signed with Apple private key)
        var clientSecret = GenerateAppleClientSecret();

        var tokenRequest = new Dictionary<string, string>
        {
            ["client_id"] = _settings.Apple.ClientId,
            ["client_secret"] = clientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
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
        if (string.IsNullOrEmpty(userInfo.Email))
        {
            throw new InvalidCredentialsException("Email is required for authentication");
        }

        var email = userInfo.Email.ToLower().Trim();

        // Check if this external login already exists
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

        // Check if user exists with this email
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Get tenant ID
        var tenantIdString = _configuration["SelfHosted:TenantId"]
            ?? "00000000-0000-0000-0000-000000000001";
        var tenantId = Guid.Parse(tenantIdString);

        if (existingUser != null)
        {
            // Link external login to existing user
            var externalLogin = new UserExternalLogin
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
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
