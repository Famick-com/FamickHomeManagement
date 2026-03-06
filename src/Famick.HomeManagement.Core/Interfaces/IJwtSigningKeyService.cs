using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Manages the RSA key lifecycle for JWT signing and validation.
/// Registered as a singleton so the same key is used for the entire app lifetime.
/// </summary>
public interface IJwtSigningKeyService
{
    /// <summary>
    /// Signing credentials for token generation (RS256)
    /// </summary>
    SigningCredentials SigningCredentials { get; }

    /// <summary>
    /// RSA security key for token validation
    /// </summary>
    RsaSecurityKey SecurityKey { get; }

    /// <summary>
    /// JSON Web Key containing the public key only, for the JWKS endpoint
    /// </summary>
    JsonWebKey JsonWebKey { get; }
}
