using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Serves the JSON Web Key Set (JWKS) for JWT token verification by external services
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
    /// Returns the JWKS containing the RSA public key used to sign JWT tokens
    /// </summary>
    [HttpGet("jwks.json")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetJwks()
    {
        var jwk = _signingKeyService.JsonWebKey;

        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = jwk.Kty,
                    use = jwk.Use,
                    kid = jwk.Kid,
                    alg = jwk.Alg,
                    n = jwk.N,
                    e = jwk.E
                }
            }
        };

        return Ok(jwks);
    }
}
