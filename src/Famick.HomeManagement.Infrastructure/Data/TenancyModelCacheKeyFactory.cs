using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Famick.HomeManagement.Infrastructure.Data;

/// <summary>
/// Keys the cached model by the tenancy options as well as the context type, since
/// <see cref="HomeManagementDbContext"/> builds different query filters for each mode.
/// </summary>
internal sealed class TenancyModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) => context is HomeManagementDbContext db
        ? (context.GetType(), db.TenancyModelKey, designTime)
        : (context.GetType(), designTime);
}
