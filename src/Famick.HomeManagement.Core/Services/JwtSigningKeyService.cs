using System.Security.Cryptography;
using Famick.HomeManagement.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Core.Services;

/// <summary>
/// Manages the RSA key lifecycle for JWT signing.
/// Reads an RSA private key from configuration, or auto-generates one for development.
/// </summary>
public class JwtSigningKeyService : IJwtSigningKeyService
{
    public SigningCredentials SigningCredentials { get; }
    public RsaSecurityKey SecurityKey { get; }
    public JsonWebKey JsonWebKey { get; }

    public JwtSigningKeyService(IConfiguration configuration, ILogger<JwtSigningKeyService> logger)
    {
        var pem = configuration["JwtSettings:RsaPrivateKeyPem"];
        RSA rsa;

        if (string.IsNullOrWhiteSpace(pem))
        {
            rsa = RSA.Create(2048);
            var generatedPem = rsa.ExportRSAPrivateKeyPem();
            logger.LogWarning(
                "JwtSettings:RsaPrivateKeyPem is not configured. Auto-generated a 2048-bit RSA key. " +
                "Set the following in your configuration to persist across restarts:\n{Pem}", generatedPem);
        }
        else
        {
            rsa = RSA.Create();
            rsa.ImportFromPem(pem);
        }

        SecurityKey = new RsaSecurityKey(rsa)
        {
            KeyId = ComputeKeyId(rsa)
        };

        SigningCredentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.RsaSha256);

        // Build a JsonWebKey containing only the public key
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(SecurityKey);
        jwk.Use = "sig";
        jwk.Alg = SecurityAlgorithms.RsaSha256;
        jwk.Kid = SecurityKey.KeyId;
        JsonWebKey = jwk;
    }

    private static string ComputeKeyId(RSA rsa)
    {
        var publicKeyBytes = rsa.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(publicKeyBytes);
        return Base64UrlEncoder.Encode(hash);
    }
}
