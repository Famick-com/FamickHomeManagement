using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Web.Services;

/// <summary>
/// Tenant provider for self-hosted mode with a fixed tenant ID.
/// User ID is resolved from HttpContext claims.
/// </summary>
public class FixedTenantProvider : ITenantProvider
{
    private readonly Guid _tenantId;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<FixedTenantProvider> _logger;

    public FixedTenantProvider(Guid tenantId, IHttpContextAccessor httpContextAccessor, ILogger<FixedTenantProvider> logger)
    {
        _tenantId = tenantId;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public Guid? TenantId => _tenantId;

    public Guid? UserId
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || user.Identity?.IsAuthenticated != true)
                return null;

            // Try "sub" first (when MapInboundClaims = false), then standard claim types
            var userIdClaim = user.FindFirst("sub")?.Value
                           ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? user.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (userIdClaim == null)
            {
                var claimTypes = string.Join(", ", user.Claims.Select(c => $"{c.Type}={c.Value}"));
                var authType = user.Identity?.AuthenticationType ?? "null";
                _logger.LogError("Could not resolve UserId from JWT claims. IsAuthenticated={IsAuth}, AuthType={AuthType}, ClaimCount={ClaimCount}, Claims=[{Claims}]",
                    user.Identity?.IsAuthenticated, authType, user.Claims.Count(), claimTypes);
            }

            return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
        }
    }

    public void SetTenantId(Guid tenantId)
    {
        // No-op in self-hosted mode - tenant ID is fixed
    }

    public void SetUserId(Guid userId)
    {
        // No-op - user ID is resolved from HttpContext claims
    }

    public void ClearTenantId()
    {
        // No-op in self-hosted mode - tenant ID is fixed
    }

    public void ClearUserId()
    {
        // No-op - user ID is resolved from HttpContext claims
    }
}
