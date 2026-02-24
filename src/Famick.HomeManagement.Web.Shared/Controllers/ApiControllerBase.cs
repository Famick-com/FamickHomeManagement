using Famick.HomeManagement.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers;

/// <summary>
/// Base class for all API controllers with common functionality
/// </summary>
[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected readonly ITenantProvider _tenantProvider;
    protected readonly ILogger _logger;

    protected ApiControllerBase(ITenantProvider tenantProvider, ILogger logger)
    {
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current tenant ID
    /// </summary>
    protected Guid TenantId
    {
        get
        {
            if (!_tenantProvider.TenantId.HasValue)
            {
                throw new InvalidOperationException("Tenant context not set");
            }
            return _tenantProvider.TenantId.Value;
        }
    }

    /// <summary>
    /// Validates access for anonymous file download endpoints.
    /// Returns the expected tenant ID if the user is authenticated or has a valid token.
    /// Returns null if access should be denied.
    /// </summary>
    /// <param name="tokenService">The file access token service</param>
    /// <param name="token">The access token from query string (may be null)</param>
    /// <param name="expectedResourceType">Expected resource type in the token (e.g., "product-image", "equipment-document")</param>
    /// <param name="expectedResourceId">Expected resource ID in the token</param>
    /// <returns>The expected tenant ID, or null if unauthorized</returns>
    protected Guid? ValidateFileAccess(
        IFileAccessTokenService tokenService,
        string? token,
        string expectedResourceType,
        Guid expectedResourceId)
    {
        // Authenticated users get tenant ID from provider
        if (User.Identity?.IsAuthenticated == true)
        {
            return TenantId;
        }

        // Anonymous users must have a valid token
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        if (!tokenService.TryParseToken(token, out var claims))
        {
            _logger.LogWarning("Invalid file access token");
            return null;
        }

        // Validate token is for the expected resource
        if (claims!.ResourceType != expectedResourceType || claims.ResourceId != expectedResourceId)
        {
            _logger.LogWarning("Token resource mismatch: expected {ExpectedType}/{ExpectedId}, got {ActualType}/{ActualId}",
                expectedResourceType, expectedResourceId, claims.ResourceType, claims.ResourceId);
            return null;
        }

        return claims.TenantId;
    }

    /// <summary>
    /// Validates that a resource belongs to the expected tenant.
    /// Use after loading a resource with IgnoreQueryFilters.
    /// </summary>
    /// <param name="resourceTenantId">The tenant ID from the loaded resource</param>
    /// <param name="expectedTenantId">The expected tenant ID (from ValidateFileAccess)</param>
    /// <returns>True if the resource belongs to the expected tenant</returns>
    protected bool ValidateTenantAccess(Guid resourceTenantId, Guid expectedTenantId)
    {
        if (resourceTenantId != expectedTenantId)
        {
            _logger.LogWarning("Resource tenant mismatch: resource belongs to {ResourceTenant}, not {ExpectedTenant}",
                resourceTenantId, expectedTenantId);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Returns a successful API response with data
    /// </summary>
    protected IActionResult ApiResponse<T>(T data, bool cache = false)
    {
        if (!cache)
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
        }

        return Ok(data);
    }

    /// <summary>
    /// Returns an empty successful API response (204 No Content)
    /// </summary>
    protected IActionResult EmptyApiResponse()
    {
        return NoContent();
    }

    /// <summary>
    /// Returns a generic error response
    /// </summary>
    protected IActionResult ErrorResponse(string message, int statusCode = 400)
    {
        return StatusCode(statusCode, new { error_message = message });
    }

    /// <summary>
    /// Returns a validation error response
    /// </summary>
    protected IActionResult ValidationErrorResponse(Dictionary<string, string[]> errors)
    {
        return BadRequest(new
        {
            error_message = "Validation failed",
            errors
        });
    }

    /// <summary>
    /// Returns a not found error response
    /// </summary>
    protected IActionResult NotFoundResponse(string message = "Resource not found")
    {
        return NotFound(new { error_message = message });
    }

    /// <summary>
    /// Returns an unauthorized error response
    /// </summary>
    protected IActionResult UnauthorizedResponse(string message = "Unauthorized")
    {
        return Unauthorized(new { error_message = message });
    }

    /// <summary>
    /// Returns a forbidden error response
    /// </summary>
    protected IActionResult ForbiddenResponse(string message = "Forbidden")
    {
        return StatusCode(403, new { error_message = message });
    }
}
