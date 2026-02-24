namespace Famick.HomeManagement.Web.Middleware;

/// <summary>
/// Middleware for tenant resolution in self-hosted mode (fixed tenant)
/// This is a placeholder that ensures the tenant context is set via FixedTenantProvider
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // In self-hosted mode, tenant is set via FixedTenantProvider in Program.cs
        // This middleware is just a placeholder for consistency with cloud version
        // The ITenantProvider is registered as scoped with a fixed GUID

        _logger.LogDebug("Tenant resolution: using fixed tenant (self-hosted mode)");

        await _next(context);
    }
}

/// <summary>
/// Extension method for registering the tenant resolution middleware
/// </summary>
public static class TenantResolutionMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantResolution(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantResolutionMiddleware>();
    }
}
