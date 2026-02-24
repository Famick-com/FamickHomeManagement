using Microsoft.AspNetCore.Authorization;

namespace Famick.HomeManagement.Web.Shared;

/// <summary>
/// Shared authorization policy definitions used by both server and client
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Configures the standard authorization policies for the application
    /// </summary>
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy("RequireAdmin", policy => policy.RequireRole("Admin"));
        options.AddPolicy("RequireEditor", policy => policy.RequireRole("Admin", "Editor"));
        options.AddPolicy("RequireViewer", policy => policy.RequireRole("Admin", "Editor", "Viewer"));
    }
}
