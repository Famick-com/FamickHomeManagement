using System.Net;
using System.Net.Http.Json;
using Famick.HomeManagement.Core.DTOs.AuthProxy;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

public class AuthProxyPairingService : IAuthProxyPairingService
{
    /// <summary>
    /// The named <see cref="IHttpClientFactory"/> client this service
    /// uses. Wired in Program.cs with the AuthProxy base URL.
    /// </summary>
    public const string HttpClientName = "AuthProxyPairing";

    /// <summary>
    /// Matches the <c>Cache-Control: max-age=300</c> header AuthProxy
    /// sets on <c>/pairing/status/{id}</c>. Settings page renders coalesce
    /// to one inbound call per 5 minutes.
    /// </summary>
    private static readonly TimeSpan BillingStatusTtl = TimeSpan.FromMinutes(5);

    private const string BillingStatusCacheKeyPrefix = "auth-proxy-billing-status:";

    private readonly HomeManagementDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly IJwtSigningKeyService _signingKeyService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthProxyPairingService> _logger;

    public AuthProxyPairingService(
        HomeManagementDbContext db,
        ITenantProvider tenantProvider,
        IJwtSigningKeyService signingKeyService,
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<AuthProxyPairingService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _signingKeyService = signingKeyService;
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthProxyPairingConfig?> GetCurrentAsync(CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set; AuthProxy pairing requires an authenticated tenant.");
        return await _db.AuthProxyPairingConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
    }

    public async Task<AuthProxyPairingResult> CompletePairingAsync(
        CompletePairingRequest request,
        string callerAdminEmail,
        string requestHostUrl,
        CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set; AuthProxy pairing requires an authenticated tenant.");

        // Refuse if already paired — admin must explicitly unpair first.
        var existing = await _db.AuthProxyPairingConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (existing is not null)
        {
            return AuthProxyPairingResult.Failure(
                AuthProxyPairingErrorCodes.AlreadyPaired,
                "This home server is already paired. Unpair first to re-pair.");
        }

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return AuthProxyPairingResult.Failure(
                AuthProxyPairingErrorCodes.MalformedInput,
                "Token and display name are required.");
        }

        var publicUrl = !string.IsNullOrWhiteSpace(request.PublicUrl)
            ? request.PublicUrl!.Trim()
            : requestHostUrl;
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            return AuthProxyPairingResult.Failure(
                AuthProxyPairingErrorCodes.MalformedInput,
                "Could not determine the home server's public URL. Set PublicUrl explicitly.");
        }

        // Public-key PEM + fingerprint come from the JWT signing key —
        // same key the home server uses to mint its own JWTs, so the
        // tunnel handshake can later prove control of the matching
        // private key.
        var rsa = _signingKeyService.SecurityKey.Rsa;
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();
        var fingerprint = _signingKeyService.JsonWebKey.Kid;

        var client = _httpClientFactory.CreateClient(HttpClientName);

        var completeRequest = new
        {
            token = request.Token,
            url = publicUrl,
            displayName = request.DisplayName.Trim(),
            publicKeyFingerprint = fingerprint,
            publicKeyPem,
        };

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("/pairing/complete", completeRequest, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "AuthProxy /pairing/complete network failure");
            return AuthProxyPairingResult.Failure(
                AuthProxyPairingErrorCodes.NetworkError,
                "Could not reach the AuthProxy server. Check your AuthProxy:BaseUrl setting and network.");
        }

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var pairedDto = await response.Content.ReadFromJsonAsync<AuthProxyCompleteResponseDto>(cancellationToken: ct);
            if (pairedDto is null)
            {
                return AuthProxyPairingResult.Failure(
                    AuthProxyPairingErrorCodes.MalformedInput,
                    "AuthProxy returned an empty success response.");
            }

            // AuthProxy:BaseUrl is the canonical URL we paired with; persist
            // it so future tunnel + lookup calls don't depend on the config
            // file at request time.
            var authProxyBaseUrl = _configuration["AuthProxy:BaseUrl"]
                ?? "https://famick-auth.up.railway.app";

            var config = new AuthProxyPairingConfig
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                AuthProxyHomeServerId = pairedDto.HomeServerId,
                AuthProxyBaseUrl = authProxyBaseUrl.TrimEnd('/'),
                PairedAdminEmail = pairedDto.AdminEmail,
                DisplayName = pairedDto.DisplayName,
                PairedAt = pairedDto.PairedAt.UtcDateTime,
            };
            _db.AuthProxyPairingConfigs.Add(config);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Paired with AuthProxy at {AuthProxyBaseUrl} as {HomeServerId}",
                authProxyBaseUrl, config.AuthProxyHomeServerId);

            return AuthProxyPairingResult.Success(config);
        }

        // 4xx — try to read AuthProxy's structured error.
        AuthProxyErrorDto? errorDto = null;
        try
        {
            errorDto = await response.Content.ReadFromJsonAsync<AuthProxyErrorDto>(cancellationToken: ct);
        }
        catch
        {
            // Body wasn't valid JSON; fall through to a generic message.
        }

        var errorCode = errorDto?.ErrorCode ?? AuthProxyPairingErrorCodes.MalformedInput;
        var message = errorDto?.Error
            ?? $"AuthProxy returned HTTP {(int)response.StatusCode} without a structured error body.";
        _logger.LogWarning(
            "AuthProxy /pairing/complete refused: HTTP {Status} errorCode={ErrorCode}",
            (int)response.StatusCode, errorCode);
        return AuthProxyPairingResult.Failure(errorCode, message);
    }

    public async Task UnpairAsync(CancellationToken ct)
    {
        var tenantId = _tenantProvider.TenantId
            ?? throw new InvalidOperationException("Tenant context is not set; AuthProxy pairing requires an authenticated tenant.");
        var existing = await _db.AuthProxyPairingConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, ct);
        if (existing is not null)
        {
            _db.AuthProxyPairingConfigs.Remove(existing);
            await _db.SaveChangesAsync(ct);
            // Drop any stale billing-status cache for the unpaired id so
            // re-pairing later doesn't serve a stale entry.
            _cache.Remove(BillingStatusCacheKeyPrefix + existing.AuthProxyHomeServerId);
            _logger.LogInformation("Unpaired from AuthProxy (was {HomeServerId})", existing.AuthProxyHomeServerId);
        }
    }

    public async Task<AuthProxyBillingStatus?> GetBillingStatusAsync(Guid homeServerId, CancellationToken ct)
    {
        var cacheKey = BillingStatusCacheKeyPrefix + homeServerId;
        if (_cache.TryGetValue<AuthProxyBillingStatus>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync($"/pairing/status/{homeServerId}", ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "AuthProxy /pairing/status/{HomeServerId} network failure; UI will render status unavailable",
                homeServerId);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "AuthProxy /pairing/status/{HomeServerId} returned HTTP {Status}; UI will render status unavailable",
                homeServerId, (int)response.StatusCode);
            return null;
        }

        AuthProxyBillingStatus? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<AuthProxyBillingStatus>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "AuthProxy /pairing/status/{HomeServerId} returned an unparseable body",
                homeServerId);
            return null;
        }

        if (body is null || string.IsNullOrEmpty(body.Status))
        {
            return null;
        }

        // Only cache successes — a transient network blip shouldn't
        // poison the cache for 5 minutes.
        _cache.Set(cacheKey, body, BillingStatusTtl);
        return body;
    }

    // --- private DTOs matching AuthProxy's wire shape ---

    private sealed class AuthProxyCompleteResponseDto
    {
        public Guid HomeServerId { get; set; }
        public string Url { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public DateTimeOffset PairedAt { get; set; }
    }

    private sealed class AuthProxyErrorDto
    {
        public string? ErrorCode { get; set; }
        public string? Error { get; set; }
    }
}
