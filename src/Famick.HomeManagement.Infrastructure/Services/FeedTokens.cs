using System.Security.Cryptography;
using System.Text;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Rules shared by the calendar (.ics) and contact (.vcf) feed tokens. Each one unlocks an
/// unauthenticated URL that a calendar or contacts app polls on someone's behalf.
/// </summary>
/// <remarks>
/// Only a hash is stored, so the URL is shown once, when the token is created, and a copy of the
/// database does not hand out working feed links. The hash must stay in step with the migration
/// that converted existing tokens: SHA-256 over UTF-8, as lowercase hex.
/// </remarks>
public static class FeedTokens
{
    public static string Generate() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    /// <summary>
    /// Revokes every feed token <paramref name="userId"/> holds, of both kinds. The caller saves.
    /// </summary>
    /// <remarks>
    /// Called when the person loses access (deactivation) or their credentials are reset: a feed
    /// URL is a standing credential, and leaving it working would carry the old access forward.
    /// Query filters are ignored because some callers, such as a reset link, run with no tenant.
    /// </remarks>
    public static async Task RevokeAllForUserAsync(
        HomeManagementDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        foreach (var token in await context.UserCalendarIcsTokens.IgnoreQueryFilters()
                     .Where(t => t.UserId == userId && !t.IsRevoked).ToListAsync(cancellationToken))
            token.IsRevoked = true;

        foreach (var token in await context.UserContactVcfTokens.IgnoreQueryFilters()
                     .Where(t => t.UserId == userId && !t.IsRevoked).ToListAsync(cancellationToken))
            token.IsRevoked = true;
    }
}
