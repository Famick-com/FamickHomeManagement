using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Serves the JSON Web Key Set (JWKS) for JWT token verification by external services
/// (mobile clients, the cloud auth proxy, the proxy.famick.com agent, etc.).
///
/// Phase 1 — publishes the current signing key plus any previous key still in its
/// rotation overlap window. Tokens signed with either active key validate successfully
/// for downstream consumers caching this endpoint.
/// </summary>
[Route(".well-known")]
[ApiController]
[AllowAnonymous]
public class JwksController : ControllerBase
{
    private readonly IJwtSigningKeyService _signingKeyService;

    public JwksController(IJwtSigningKeyService signingKeyService)
    {
        _signingKeyService = signingKeyService;
    }

    /// <summary>
    /// Returns the JWKS containing the active RSA public keys used to verify JWTs.
    /// During a rotation overlap, returns both current and previous keys so downstream
    /// caches converge on the new key set within the cache TTL.
    /// </summary>
    [HttpGet("jwks.json")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetJwks()
    {
        var keys = _signingKeyService.ActiveJwks
            .Select(jwk => new
            {
                kty = jwk.Kty,
                use = jwk.Use,
                kid = jwk.Kid,
                alg = jwk.Alg,
                n = jwk.N,
                e = jwk.E
            })
            .ToArray();

        // Phase 1 — publish a 5-minute cache hint so downstream verifiers don't
        // hammer this endpoint, but still pick up rotation events well within the
        // 24-hour overlap window.
        Response.Headers.CacheControl = "public, max-age=300";

        return Ok(new { keys });
    }
}
