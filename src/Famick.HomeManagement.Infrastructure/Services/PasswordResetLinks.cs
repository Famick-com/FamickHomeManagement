using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Rules shared by the paths that reset a password, whether a member follows a link or an admin
/// resets the account.
/// </summary>
internal static class PasswordResetLinks
{
    /// <summary>
    /// Marks every unused reset link <paramref name="userId"/> holds as used. The caller saves.
    /// </summary>
    /// <remarks>
    /// Once the password has been reset, a link emailed earlier should not be able to change it
    /// again. Query filters are ignored because following a reset link runs with no tenant.
    /// </remarks>
    public static async Task RevokeAllForUserAsync(
        HomeManagementDbContext context, Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        foreach (var link in await context.PasswordResetTokens.IgnoreQueryFilters()
                     .Where(t => t.UserId == userId && t.UsedAt == null).ToListAsync(cancellationToken))
        {
            link.UsedAt = now;
            link.UpdatedAt = now;
        }
    }
}
