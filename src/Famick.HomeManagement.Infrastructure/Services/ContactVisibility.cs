using System.Linq.Expressions;
using Famick.HomeManagement.Domain.Entities;
using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>
/// Which contacts a household member may see.
/// </summary>
/// <remarks>
/// One rule, used everywhere contacts are read — the contact service, the VCF feed and the
/// dietary profile — so that a private contact cannot leak through whichever path forgot to
/// check. A contact is visible when it is shared with the household, when the reader created it,
/// or when it is shared with the reader by name. Admins have no exception: "private" means
/// private from everybody but its creator.
/// </remarks>
public static class ContactVisibility
{
    /// <summary>
    /// The predicate for <paramref name="userId"/>. With no user, only household-shared contacts.
    /// </summary>
    public static Expression<Func<Contact, bool>> VisibleTo(Guid? userId) =>
        c => c.Visibility == ContactVisibilityLevel.TenantShared
          || (userId != null && c.CreatedByUserId == userId)
          || (userId != null
              && c.Visibility == ContactVisibilityLevel.SharedWithUsers
              && c.SharedWithUsers.Any(s => s.SharedWithUserId == userId));

    /// <summary>
    /// The contacts <paramref name="userId"/> may change: as <see cref="VisibleTo"/>, except a
    /// share counts only when it carries edit rights. Role is checked separately, by the caller.
    /// </summary>
    public static Expression<Func<Contact, bool>> EditableBy(Guid? userId) =>
        c => c.Visibility == ContactVisibilityLevel.TenantShared
          || (userId != null && c.CreatedByUserId == userId)
          || (userId != null
              && c.Visibility == ContactVisibilityLevel.SharedWithUsers
              && c.SharedWithUsers.Any(s => s.SharedWithUserId == userId && s.CanEdit));
}
