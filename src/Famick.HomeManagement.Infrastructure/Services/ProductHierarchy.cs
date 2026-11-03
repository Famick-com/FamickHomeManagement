using Famick.HomeManagement.Core.Exceptions;
using Famick.HomeManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Products nest one level deep, a parent and its variants. The editor only offers a parent that has none
/// of its own, and offers none to a product with variants; every path that sets a parent holds to the same
/// rule, which also rules out a cycle.
/// </summary>
internal static class ProductHierarchy
{
    /// <summary>
    /// Throws when <paramref name="parentId"/> can't be the parent of <paramref name="productId"/>, which is
    /// null for a product being created.
    /// </summary>
    public static async Task EnsureValidParentAsync(
        HomeManagementDbContext context, Guid? productId, Guid parentId, CancellationToken cancellationToken)
    {
        if (parentId == productId)
            throw new BusinessRuleViolationException("ParentProduct", "A product can't be its own parent");

        var parent = await context.Products
            .Where(p => p.Id == parentId)
            .Select(p => new { p.ParentProductId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleViolationException("ParentProduct", "The parent product doesn't exist");
        if (parent.ParentProductId != null)
            throw new BusinessRuleViolationException("ParentProduct", "A variant can't be a parent product");

        if (productId is { } id && await context.Products.AnyAsync(p => p.ParentProductId == id, cancellationToken))
            throw new BusinessRuleViolationException("ParentProduct", "A product with variants can't be a variant");
    }
}
