using Famick.HomeManagement.Core.DTOs.Authentication;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;
using Famick.HomeManagement.Infrastructure.Configuration;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// The parts of a sign-in response that depend on the household and the deployment
/// rather than on how the user proved who they are. Every path that issues a session
/// (password, passkey, Google/Apple/OIDC, refresh, reauth) reads them from here so
/// they cannot drift between paths.
/// </summary>
public static class LoginResponseHelper
{
    /// <summary>
    /// Whether the user has to accept the current legal terms before using the app:
    /// they never accepted, or accepted an older version than
    /// <c>LegalTerms:CurrentVersion</c>. Only cloud publishes a version, so this is
    /// always false on self-hosted.
    /// </summary>
    public static bool MustAcceptTerms(User user, IMultiTenancyOptions multiTenancy, IConfiguration configuration)
    {
        if (!multiTenancy.IsMultiTenantEnabled)
        {
            return false;
        }

        var currentVersion = configuration["LegalTerms:CurrentVersion"];
        if (string.IsNullOrEmpty(currentVersion))
        {
            return false;
        }

        return user.TermsAcceptedAt == null || user.TermsVersion != currentVersion;
    }

    /// <summary>
    /// The user's household with its subscription state, which the clients use for
    /// feature gating. Self-hosted reports itself unrestricted, with no tier; cloud
    /// reports what the tenant row says.
    /// </summary>
    public static async Task<TenantInfoDto> BuildTenantInfoAsync(
        HomeManagementDbContext context,
        User user,
        IMultiTenancyOptions multiTenancy,
        CancellationToken cancellationToken)
    {
        var tenant = await context.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);

        var tenantDto = new TenantInfoDto
        {
            Id = tenant?.Id ?? user.TenantId,
            Name = tenant?.Name ?? string.Empty,
            Subdomain = string.Empty,
        };

        if (!multiTenancy.IsMultiTenantEnabled)
        {
            tenantDto.SubscriptionTier = string.Empty;
            tenantDto.IsUnrestricted = true;
            tenantDto.IsTrialActive = false;
            tenantDto.IsExpired = false;
        }
        else if (tenant != null)
        {
            tenantDto.SubscriptionTier = tenant.SubscriptionTier.ToString();
            tenantDto.IsTrialActive = tenant.IsTrialActive;
            tenantDto.TrialEndsAt = tenant.TrialEndsAt;
            tenantDto.IsExpired = tenant.SubscriptionTier == SubscriptionTier.Free
                && !tenant.IsTrialActive;
        }

        return tenantDto;
    }
}
