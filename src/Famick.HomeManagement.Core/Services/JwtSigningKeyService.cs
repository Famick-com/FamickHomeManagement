using System.Globalization;
using System.Security.Cryptography;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// Manages the RSA key lifecycle for JWT signing and validation. Phase 1 supports
/// dual-key rotation: a "current" signing key plus an optional "previous" verification-only
/// key during a 24-hour overlap window so tokens signed just before a rotation still
/// validate after the cutover.
///
/// Configuration shape (preferred):
/// <code>
/// JwtSettings:
///   CurrentKey:
///     RsaPrivateKeyPem: ...        (or RsaPrivateKeyPemFile)
///   PreviousKey:                   (optional, present during rotation overlap)
///     RsaPrivateKeyPem: ...        (or RsaPrivateKeyPemFile)
///     RetiresAt: 2026-06-15T00:00:00Z
/// </code>
///
/// Legacy shape (back-compat — single key, no rotation): <c>JwtSettings:RsaPrivateKeyPem</c>
/// or <c>JwtSettings:RsaPrivateKeyPemFile</c>. If <c>CurrentKey</c> is empty and the
/// legacy field is set, the service loads the legacy key as the current key with no
/// previous key.
///
/// In dev (no config at all), an ephemeral 2048-bit key is generated and logged, so
/// the app boots; this matches the pre-Phase-1 behavior.
/// </summary>
public class JwtSigningKeyService : IJwtSigningKeyService
{
    public SigningCredentials SigningCredentials { get; }
    public RsaSecurityKey SecurityKey { get; }
    public JsonWebKey JsonWebKey { get; }
    public IReadOnlyList<RsaSecurityKey> ActiveValidationKeys { get; }
    public IReadOnlyList<JsonWebKey> ActiveJwks { get; }

    public JwtSigningKeyService(
        IConfiguration configuration,
        ILogger<JwtSigningKeyService> logger,
        IHostEnvironment? hostEnvironment = null)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");

        // Production refuses the auto-generate fallback — an ephemeral key
        // looks fine until the container restarts, at which point every
        // already-issued token fails validation and the AuthProxy tunnel
        // handshake breaks with public_key_mismatch. Dev / Staging / tests
        // (hostEnvironment == null) keep the auto-gen ergonomics.
        var isProduction = hostEnvironment?.IsProduction() ?? false;

        // Resolve the current key from new shape, legacy shape, or auto-generate.
        var currentRsa = LoadKey(
            jwtSettings.GetSection("CurrentKey:RsaPrivateKeyPem").Value,
            jwtSettings.GetSection("CurrentKey:RsaPrivateKeyPemFile").Value,
            logger,
            "current",
            allowAutoGenerate: !isProduction,
            legacyPemFallback: jwtSettings["RsaPrivateKeyPem"],
            legacyPemFileFallback: jwtSettings["RsaPrivateKeyPemFile"]);

        var currentKey = BuildKey(currentRsa);
        SecurityKey = currentKey;
        SigningCredentials = new SigningCredentials(currentKey, SecurityAlgorithms.RsaSha256);
        JsonWebKey = BuildJwk(currentKey);

        // Optional previous key for rotation overlap.
        var previousKey = TryLoadPreviousKey(jwtSettings.GetSection("PreviousKey"), logger);

        var validationKeys = new List<RsaSecurityKey> { currentKey };
        var jwks = new List<JsonWebKey> { JsonWebKey };
        if (previousKey is not null)
        {
            validationKeys.Add(previousKey);
            jwks.Add(BuildJwk(previousKey));
            logger.LogInformation(
                "JWT signing key rotation overlap active — previous key {Kid} accepted for validation",
                previousKey.KeyId);
        }

        ActiveValidationKeys = validationKeys;
        ActiveJwks = jwks;
    }

    private static RSA LoadKey(
        string? configuredPem,
        string? configuredPemFile,
        ILogger logger,
        string label,
        bool allowAutoGenerate,
        string? legacyPemFallback = null,
        string? legacyPemFileFallback = null)
    {
        var pem = configuredPem;
        var pemFile = configuredPemFile;

        // Fall back to the legacy single-key shape for back-compat with pre-Phase-1
        // configurations that haven't been migrated to the CurrentKey/PreviousKey form.
        if (string.IsNullOrWhiteSpace(pem) && string.IsNullOrWhiteSpace(pemFile))
        {
            pem = legacyPemFallback;
            pemFile = legacyPemFileFallback;
        }

        if (string.IsNullOrWhiteSpace(pem) && !string.IsNullOrWhiteSpace(pemFile))
        {
            if (!File.Exists(pemFile))
            {
                throw new FileNotFoundException(
                    $"JWT RSA {label} key file not found: {pemFile}");
            }
            pem = File.ReadAllText(pemFile);
            logger.LogInformation("Loaded RSA {Label} signing key from file: {Path}", label, pemFile);
        }

        if (string.IsNullOrWhiteSpace(pem))
        {
            if (!allowAutoGenerate)
            {
                // For "current" this fires in Production — ephemeral keys break
                // tunnel handshakes and invalidate every issued token on restart,
                // so we refuse to start. For "previous" this just means the
                // optional rotation overlap wasn't configured.
                var message = label == "current"
                    ? "JwtSettings:CurrentKey:RsaPrivateKeyPem (or RsaPrivateKeyPemFile) is not configured. " +
                      "An ephemeral auto-generated key would silently break the AuthProxy tunnel and invalidate " +
                      "every issued JWT on the next container restart. For self-hosted Docker, generate a key file " +
                      "on the host and mount it: " +
                      "`openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out /opt/famick/data/keys/jwt-rsa.pem` " +
                      "+ env var `JwtSettings__RsaPrivateKeyPemFile=/app/data/keys/jwt-rsa.pem`."
                    : $"JwtSettings:{label}Key:RsaPrivateKeyPem is not configured";
                throw new InvalidOperationException(message);
            }
            var rsa = RSA.Create(2048);
            var generatedPem = rsa.ExportRSAPrivateKeyPem();
            logger.LogWarning(
                "JwtSettings:CurrentKey:RsaPrivateKeyPem is not configured. Auto-generated a 2048-bit RSA key. " +
                "Set the following in your configuration to persist across restarts:\n{Pem}", generatedPem);
            return rsa;
        }
        else
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return rsa;
        }
    }

    private static RsaSecurityKey BuildKey(RSA rsa) =>
        new(rsa) { KeyId = ComputeKeyId(rsa) };

    private static JsonWebKey BuildJwk(RsaSecurityKey securityKey)
    {
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(securityKey);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        jwk.Kid = securityKey.KeyId;
        return jwk;
    }

    private static RsaSecurityKey? TryLoadPreviousKey(IConfigurationSection section, ILogger logger)
    {
        var pem = section["RsaPrivateKeyPem"];
        var pemFile = section["RsaPrivateKeyPemFile"];

        if (string.IsNullOrWhiteSpace(pem) && string.IsNullOrWhiteSpace(pemFile))
        {
            return null;
        }

        // RetiresAt gates whether the previous key is still active. After the
        // configured timestamp the previous key is silently dropped from the
        // active set even though it remains in configuration.
        var retiresAtRaw = section["RetiresAt"];
        if (!string.IsNullOrWhiteSpace(retiresAtRaw))
        {
            if (!DateTimeOffset.TryParse(retiresAtRaw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var retiresAt))
            {
                logger.LogWarning(
                    "JwtSettings:PreviousKey:RetiresAt is set but unparseable: '{RetiresAt}' — ignoring previous key",
                    retiresAtRaw);
                return null;
            }
            if (retiresAt <= DateTimeOffset.UtcNow)
            {
                logger.LogInformation(
                    "JwtSettings:PreviousKey:RetiresAt {RetiresAt} has passed — previous key not loaded",
                    retiresAt);
                return null;
            }
        }

        var rsa = LoadKey(pem, pemFile, logger, "previous", allowAutoGenerate: false);
        return BuildKey(rsa);
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyBytes);
        return Base64UrlEncoder.Encode(hash);
    }
}
