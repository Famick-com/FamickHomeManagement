using Microsoft.IdentityModel.Tokens;

namespace Famick.HomeManagement.Core.Interfaces;

/// <summary>
/// Manages the RSA key lifecycle for JWT signing and validation.
/// Registered as a singleton so the same key set is used for the app lifetime.
///
/// Phase 1 — supports JWKS rotation. The service loads a "current" signing key plus
/// an optional "previous" key during the rotation overlap window. New tokens are
/// always signed with the current key; tokens signed with either active key validate
/// successfully until the previous key's <c>RetiresAt</c> passes.
/// </summary>
public interface IJwtSigningKeyService
{
    /// <summary>
    /// Signing credentials for the **current** key (used to sign newly-issued tokens).
    /// </summary>
    SigningCredentials SigningCredentials { get; }

    /// <summary>
    /// The current key's <see cref="RsaSecurityKey"/>. Equivalent to the first entry
    /// in <see cref="ActiveValidationKeys"/>.
    /// </summary>
    RsaSecurityKey SecurityKey { get; }

    /// <summary>
    /// JSON Web Key for the current key only. Kept for back-compat with consumers
    /// that wanted a single key — for the JWKS endpoint payload, prefer
    /// <see cref="ActiveJwks"/>, which includes the previous key during overlap.
    /// </summary>
    JsonWebKey JsonWebKey { get; }

    /// <summary>
    /// Every key the validator should accept right now: the current key plus the
    /// previous key if it is still inside its overlap window. Pass this collection
    /// to <c>TokenValidationParameters.IssuerSigningKeys</c>.
    /// </summary>
    IReadOnlyList<RsaSecurityKey> ActiveValidationKeys { get; }

    /// <summary>
    /// Every key to publish at <c>/.well-known/jwks.json</c>: same set as
    /// <see cref="ActiveValidationKeys"/>, in JsonWebKey form. Iterating this is what
    /// the JwksController returns as the <c>keys</c> array.
    /// </summary>
    IReadOnlyList<JsonWebKey> ActiveJwks { get; }
}
